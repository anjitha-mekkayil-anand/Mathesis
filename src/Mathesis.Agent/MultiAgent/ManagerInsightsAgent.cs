using ModelContextProtocol.Client;
using OpenAI.Chat;

namespace Mathesis.Agent.MultiAgent;

public sealed record InsightsOutcome(string Report);

/// <summary>
/// Agent 4 — Manager Insights. Produces the team-level readiness view: progress by
/// role and certification track, capacity-constrained learners, and exam-risk areas.
/// Read-only tool surface — it reports to the manager; it decides nothing, and it
/// presents insights without exposing sensitive personal detail.
/// </summary>
public sealed class ManagerInsightsAgent
{
    private static readonly HashSet<string> AllowedTools =
        ["list_learners", "get_learner_readiness", "get_learning_history"];

    private const string SystemPrompt =
        "You are the Manager Insights Agent — the team-level reporting agent in an enterprise " +
        "learning team. Your only job is visibility: summarise certification readiness across the " +
        "team for their manager. " +
        "For each learner, use get_learner_readiness; aggregate into: overall team readiness, " +
        "who is ready to book, who is on track, and who is at risk. " +
        "Flag capacity-constrained learners (heavy meeting load, low focus hours) as a scheduling " +
        "problem for the manager to solve — not a learner failure. " +
        "Compare against historical outcomes where useful. " +
        "Present insights respectfully and without exposing sensitive personal detail. " +
        "Conclude with a concise team readiness report: summary line, per-learner status table, " +
        "risk areas, and the one action the manager should take this week.";

    public async Task<InsightsOutcome> ReportAsync(
        ChatClient chatClient,
        McpClientWrapper mcp,
        IList<McpClientTool> mcpTools,
        CancellationToken ct)
    {
        var userMessage =
            "Produce the team readiness report. Call list_learners, then get_learner_readiness for " +
            "each learner, and get_learning_history for context. Conclude with the report.";

        var outcome = await FoundryChatLoop.RunAsync(
            chatClient, mcp, mcpTools, AllowedTools, SystemPrompt, userMessage, ct);

        var report = outcome.FinalTextWasToolJson || string.IsNullOrWhiteSpace(outcome.FinalText)
            ? "Report unavailable — see readiness data in the dashboard."
            : outcome.FinalText;

        return new InsightsOutcome(report);
    }
}
