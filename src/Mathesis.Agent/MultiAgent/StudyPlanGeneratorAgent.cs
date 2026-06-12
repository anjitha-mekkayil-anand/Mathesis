using ModelContextProtocol.Client;
using OpenAI.Chat;

namespace Mathesis.Agent.MultiAgent;

public sealed record PlanOutcome(bool PlanProposed, string PlanSummary);

/// <summary>
/// Agent 2 — Study Plan Generator. Converts the curated path into a practical,
/// capacity-aware schedule using the learner's work-activity signals (the Work IQ
/// concept layer) and historical outcomes. Its one write is propose_study_plan,
/// which lands in the manager approval queue as 'pending' — the agent cannot
/// activate a plan.
/// </summary>
public sealed class StudyPlanGeneratorAgent
{
    private static readonly HashSet<string> AllowedTools =
        ["list_learners", "get_learning_history", "propose_study_plan"];

    private const string SystemPrompt =
        "You are the Study Plan Generator — the second agent in an enterprise learning team. " +
        "The Learning Path Curator has already selected the content; your only job is converting " +
        "it into a practical, capacity-aware study schedule. " +
        "Use the learner's work-activity signals (meeting hours, focus hours, preferred learning slot) " +
        "to choose realistic study windows: planned weekly study hours should not exceed half of the " +
        "learner's weekly focus hours, and sessions belong in the learner's preferred slot. " +
        "Use get_learning_history to ground total hours in what actually worked for past learners of " +
        "the same certification. " +
        "Sequence the heaviest-weighted domain first; place the weakest domain in both the first and " +
        "final weeks; reserve the final week for practice assessments. " +
        "Record the plan by calling propose_study_plan exactly once, with the concrete weekly schedule " +
        "and a rationale that cites the work signals, historical outcomes, and source documents used. " +
        "The plan enters a manager approval queue — note that in your rationale.";

    public async Task<PlanOutcome> PlanAsync(
        ChatClient chatClient,
        McpClientWrapper mcp,
        IList<McpClientTool> mcpTools,
        string learnerId,
        string certificationId,
        string curatedPath,
        string? groundedContext,
        CancellationToken ct)
    {
        var systemPrompt = groundedContext is null
            ? SystemPrompt
            : $"{SystemPrompt}\n\n{groundedContext}";

        var userMessage =
            $"Create a study plan for learner '{learnerId}' targeting '{certificationId}'.\n\n" +
            $"Curated learning path from the Curator:\n{curatedPath}\n\n" +
            "Call list_learners for the learner's work-activity signals, get_learning_history for " +
            $"'{certificationId}' outcomes, then call propose_study_plan with the schedule and rationale.";

        var outcome = await FoundryChatLoop.RunAsync(
            chatClient, mcp, mcpTools, AllowedTools, systemPrompt, userMessage, ct);

        var proposeCall = outcome.ToolCalls.LastOrDefault(c => c.Name == "propose_study_plan");

        return new PlanOutcome(
            proposeCall is not null,
            proposeCall?.Args.GetValueOrDefault("plan")
                ?? (outcome.FinalTextWasToolJson ? "Plan recorded — see approval queue." : outcome.FinalText));
    }
}
