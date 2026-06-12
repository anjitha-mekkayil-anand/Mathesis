using Mathesis.Agent.FoundryIq;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using System.Text.Json;

namespace Mathesis.Agent.MultiAgent;

/// <summary>
/// Runs the learning pipeline over one or more learners, sharing one MCP session and
/// one chat client. Per learner:
///
///   deterministic readiness (pre-filter, no LLM)
///     ├─ Ready      → Assessment Agent → next step (book / final check)
///     └─ Borderline / NotReady
///          → Curator → Study Plan Generator (plan lands in manager approval queue)
///          → HUMAN GATE: "ready to be assessed?" (the learner decides — interactive
///            prompt; in non-interactive runs Borderline assesses, NotReady defers)
///          → Assessment Agent, or a deterministic defer recommendation
///
/// After all learners, the Manager Insights Agent reports team-level readiness.
/// Two human gates total: the learner gates the assessment, the manager gates the plan.
/// </summary>
public sealed class LearningPipelineOrchestrator(
    IOptions<AgentOptions> options,
    FoundryIqGroundingService grounding)
{
    public async Task<IReadOnlyList<LearnerAnalysisResult>> RunAsync(
        IReadOnlyList<string> learnerIds,
        bool interactive,
        bool includeManagerReport,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        var azureOpts = opts.AzureFoundry;

        if (string.IsNullOrEmpty(azureOpts.ApiKey))
            throw new InvalidOperationException(
                "AzureFoundry:ApiKey is empty. Set via user secrets: " +
                "dotnet user-secrets set \"LearningAgents:AzureFoundry:ApiKey\" \"<key>\" --project src/Mathesis.Runner");

        if (string.IsNullOrEmpty(azureOpts.Endpoint))
            throw new InvalidOperationException("AzureFoundry:Endpoint is empty.");

        await using var mcp = await McpClientWrapper.CreateAsync(
            opts.McpServerPath, opts.McpWorkingDirectory, ct);

        var mcpTools = await mcp.ListToolsAsync(ct);

        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(azureOpts.Endpoint) };
        var chatClient = new OpenAIClient(new ApiKeyCredential(azureOpts.ApiKey), clientOptions)
            .GetChatClient(azureOpts.DeploymentName);

        var results = new List<LearnerAnalysisResult>();

        foreach (var learnerId in learnerIds)
        {
            // Deterministic pre-filter — no LLM involved.
            var readinessJson = await mcp.CallToolAsync(
                "get_learner_readiness", new Dictionary<string, object?> { ["learner_id"] = learnerId }, ct);
            var readiness = ParseReadiness(readinessJson);

            if (readiness is null)
            {
                Narrate.Line("[Readiness]", $"{learnerId}: not found in roster — skipped.", ConsoleColor.DarkCyan, ConsoleColor.DarkGray);
                continue;
            }

            Narrate.Line("[Readiness]", $"{learnerId}: {readiness.Score}/100 ({readiness.Band}) — deterministic pre-filter, no LLM",
                ConsoleColor.DarkCyan, Narrate.BandColor(readiness.Band));

            // One Foundry IQ retrieval per learner, shared by every agent in the pass.
            var groundedContext = await grounding.GetGroundedContextAsync(
                readiness.CertificationId, readiness.WeakDomains, ct);

            string? curatedPath = null;
            var planProposed = false;
            string? assessmentSummary = null;
            string nextStep;

            if (readiness.Band == "Ready")
            {
                Narrate.Line("[Curator]", $"skipped — {learnerId} is Ready; no new content needed.", ConsoleColor.Cyan, ConsoleColor.DarkGray);
                Narrate.Line("[Planner]", "skipped — no plan needed for a Ready learner.", ConsoleColor.Cyan, ConsoleColor.DarkGray);
                (assessmentSummary, nextStep) = await RunAssessmentAsync(
                    chatClient, mcp, mcpTools, learnerId, readiness, groundedContext, ct);
            }
            else
            {
                Narrate.Line("[Curator]", $"curating learning path for weak domains: {string.Join(", ", readiness.WeakDomains)}...");
                var curation = await new LearningPathCuratorAgent().CurateAsync(
                    chatClient, mcp, mcpTools, learnerId, groundedContext, ct);
                curatedPath = curation.CuratedPath;

                Narrate.Line("[Planner]", "generating capacity-aware study plan...");
                var plan = await new StudyPlanGeneratorAgent().PlanAsync(
                    chatClient, mcp, mcpTools, learnerId, readiness.CertificationId, curatedPath, groundedContext, ct);
                planProposed = plan.PlanProposed;
                if (planProposed)
                    Narrate.Line("[Planner]", "plan recorded — status: pending manager approval.", ConsoleColor.Cyan, ConsoleColor.DarkYellow);
                else
                    Narrate.Line("[Planner]", "WARNING: no plan was recorded.", ConsoleColor.Cyan, ConsoleColor.Yellow);

                // Human gate #1: the learner decides whether to be assessed now.
                var assessNow = AskHumanGate(learnerId, readiness, interactive);

                if (assessNow)
                {
                    (assessmentSummary, nextStep) = await RunAssessmentAsync(
                        chatClient, mcp, mcpTools, learnerId, readiness, groundedContext, ct);
                }
                else
                {
                    Narrate.Line("[Assessor]", "deferred — learner not ready to be assessed.", ConsoleColor.Cyan, ConsoleColor.DarkGray);
                    nextStep = $"Follow the proposed study plan (pending manager approval) and reassess " +
                               $"after completing it. Current readiness: {readiness.Score}/100.";
                    await mcp.CallToolAsync("recommend_next_step", new Dictionary<string, object?>
                    {
                        ["learner_id"] = learnerId,
                        ["recommendation"] = nextStep,
                        ["readiness_score"] = readiness.Score
                    }, ct);
                }
            }

            Console.WriteLine();
            results.Add(new LearnerAnalysisResult(
                learnerId, readiness.CertificationId, readiness.Score, readiness.Band,
                curatedPath, planProposed, assessmentSummary, nextStep, DateTimeOffset.UtcNow));
        }

        if (includeManagerReport)
        {
            Narrate.Line("[Manager]", "compiling team readiness report...");
            var insights = await new ManagerInsightsAgent().ReportAsync(chatClient, mcp, mcpTools, ct);
            Console.WriteLine();
            Console.WriteLine("=== Manager Insights — Team Readiness Report ===");
            Console.WriteLine(insights.Report);
            Console.WriteLine();
        }

        return results;
    }

    private static async Task<(string Summary, string NextStep)> RunAssessmentAsync(
        OpenAI.Chat.ChatClient chatClient,
        McpClientWrapper mcp,
        IList<ModelContextProtocol.Client.McpClientTool> mcpTools,
        string learnerId,
        ReadinessSnapshot readiness,
        string? groundedContext,
        CancellationToken ct)
    {
        Narrate.Line("[Assessor]", "generating cited questions + readiness verdict...");
        var assessment = await new AssessmentAgent().AssessAsync(
            chatClient, mcp, mcpTools, learnerId, readiness.CertificationId, groundedContext, ct);
        Narrate.Line("[Assessor]", "next step recorded.", ConsoleColor.Cyan, ConsoleColor.DarkYellow);
        return (assessment.Summary, assessment.NextStep);
    }

    /// <summary>Human gate #1 from the suggested architecture: "Ready to be assessed?"</summary>
    private static bool AskHumanGate(string learnerId, ReadinessSnapshot readiness, bool interactive)
    {
        if (!interactive)
        {
            // Non-interactive default: Borderline learners are assessed, NotReady defer.
            var assess = readiness.Band == "Borderline";
            Narrate.Line("[Human gate]", $"non-interactive — {(assess ? "assessing" : "deferring assessment for")} {readiness.Band} learner.", ConsoleColor.Yellow);
            return assess;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{"[Human gate]",-15} ");
        Console.ResetColor();
        Console.Write($"{learnerId} is {readiness.Band} ({readiness.Score}/100). Ready to be assessed now? [y/N] ");
        var answer = Console.ReadLine()?.Trim();
        return answer is not null && answer.StartsWith("y", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ReadinessSnapshot(
        string CertificationId, double Score, string Band, IReadOnlyList<string> WeakDomains);

    private static ReadinessSnapshot? ParseReadiness(string readinessJson)
    {
        using var doc = JsonDocument.Parse(readinessJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out _))
            return null;

        var weakDomains = new List<string>();
        if (root.TryGetProperty("weak_domains", out var weak))
        {
            foreach (var domain in weak.EnumerateArray())
            {
                if (domain.TryGetProperty("Name", out var name) && name.GetString() is { } domainName)
                    weakDomains.Add(domainName);
            }
        }

        return new ReadinessSnapshot(
            root.GetProperty("certification_id").GetString() ?? "",
            root.GetProperty("readiness_score").GetDouble(),
            root.GetProperty("band").GetString() ?? "NotReady",
            weakDomains);
    }
}
