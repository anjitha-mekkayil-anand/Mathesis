using ModelContextProtocol.Client;
using OpenAI.Chat;

namespace Mathesis.Agent.MultiAgent;

public sealed record AssessmentOutcome(string Summary, string NextStep);

/// <summary>
/// Agent 3 — Assessment Agent. Generates grounded, cited practice questions targeting
/// the learner's weak domains, interprets readiness against the pass threshold, and
/// records the final next-step recommendation. Writes: record_assessment and
/// recommend_next_step. It cannot mark anyone certified and cannot approve plans.
/// </summary>
public sealed class AssessmentAgent
{
    private static readonly HashSet<string> AllowedTools =
        ["get_learner_readiness", "get_certification", "record_assessment", "recommend_next_step"];

    private const string SystemPrompt =
        "You are the Assessment Agent — the readiness evaluator in an enterprise learning team. " +
        "Your job has two steps. " +
        "Step 1 — Assess: generate exactly 5 scenario-based practice questions for the learner's " +
        "certification, weighted toward their weak domains and the certification's heaviest domains. " +
        "Every question must cite the source document it derives from. Record them with " +
        "record_assessment, including your readiness evaluation against the pass threshold. " +
        "Step 2 — Recommend: based on the readiness score, practice score, and pass threshold, " +
        "decide the next step: book the exam, focused revision on specific domains, or extend the " +
        "study plan. Record it by calling recommend_next_step with the readiness score — always call " +
        "this as your final tool call. " +
        "Be specific and honest: recommending a premature exam booking wastes the learner's " +
        "confidence and the organisation's money.";

    public async Task<AssessmentOutcome> AssessAsync(
        ChatClient chatClient,
        McpClientWrapper mcp,
        IList<McpClientTool> mcpTools,
        string learnerId,
        string certificationId,
        string? groundedContext,
        CancellationToken ct)
    {
        var systemPrompt = groundedContext is null
            ? SystemPrompt
            : $"{SystemPrompt}\n\n{groundedContext}";

        var userMessage =
            $"Assess learner '{learnerId}' for '{certificationId}'. " +
            "Call get_learner_readiness and get_certification first. Generate the 5 cited questions, " +
            "record them with record_assessment, then call recommend_next_step with your final " +
            "recommendation and the readiness score.";

        var outcome = await FoundryChatLoop.RunAsync(
            chatClient, mcp, mcpTools, AllowedTools, systemPrompt, userMessage, ct);

        var recommendCall = outcome.ToolCalls.LastOrDefault(c => c.Name == "recommend_next_step");
        var assessCall = outcome.ToolCalls.LastOrDefault(c => c.Name == "record_assessment");

        var summary = assessCall?.Args.GetValueOrDefault("evaluation")
            ?? (outcome.FinalTextWasToolJson ? "Assessment recorded." : outcome.FinalText);

        var nextStep = recommendCall?.Args.GetValueOrDefault("recommendation")
            ?? "No recommendation recorded — review manually.";

        return new AssessmentOutcome(summary, nextStep);
    }
}
