using ModelContextProtocol.Client;
using OpenAI.Chat;

namespace Mathesis.Agent.MultiAgent;

public sealed record CurationOutcome(string CuratedPath);

/// <summary>
/// Agent 1 — Learning Path Curator. Maps the learner's certification target and weak
/// domains to relevant content, grounded in the Foundry IQ knowledge base. Read-only
/// tool surface: it can look at the learner and the certification; it cannot write
/// anything. Cited content over free-text recommendations.
/// </summary>
public sealed class LearningPathCuratorAgent
{
    private static readonly HashSet<string> AllowedTools =
        ["get_learner_readiness", "get_certification"];

    private const string SystemPrompt =
        "You are the Learning Path Curator — the first agent in an enterprise learning team " +
        "that prepares employees for internal certification programmes. " +
        "Your only job is curation: map the learner's target certification and weak domains " +
        "to the most relevant learning content. " +
        "Prioritise the learner's weak domains (self-rated 3 or below) and the certification's " +
        "heaviest-weighted domains. " +
        "Return cited content: every recommendation must name the source document it comes from. " +
        "Do NOT produce a study schedule — that is the Study Plan Generator's job. " +
        "Conclude with a concise curated learning path: per domain, what to study and from which source.";

    public async Task<CurationOutcome> CurateAsync(
        ChatClient chatClient,
        McpClientWrapper mcp,
        IList<McpClientTool> mcpTools,
        string learnerId,
        string? groundedContext,
        CancellationToken ct)
    {
        var systemPrompt = groundedContext is null
            ? SystemPrompt
            : $"{SystemPrompt}\n\n{groundedContext}";

        var userMessage =
            $"Curate a learning path for learner '{learnerId}'. " +
            "Call get_learner_readiness to see their weak domains, then get_certification for the " +
            "domain weights. Map each weak domain to specific content, citing the source document. " +
            "Conclude with the curated path.";

        var outcome = await FoundryChatLoop.RunAsync(
            chatClient, mcp, mcpTools, AllowedTools, systemPrompt, userMessage, ct);

        var curatedPath = outcome.FinalTextWasToolJson || string.IsNullOrWhiteSpace(outcome.FinalText)
            ? "Curated path unavailable — see study plan rationale."
            : outcome.FinalText;

        return new CurationOutcome(curatedPath);
    }
}
