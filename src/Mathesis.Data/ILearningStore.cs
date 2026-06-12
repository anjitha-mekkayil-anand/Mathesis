using Mathesis.Core;

namespace Mathesis.Data;

/// <summary>Write side: what the agents are allowed to record. Note what is absent —
/// there is no approve/activate method here; approval belongs to the manager (human).</summary>
public interface ILearningStore
{
    Task RecordStudyPlanAsync(string learnerId, string certificationId, string plan, string rationale, CancellationToken ct = default);
    Task RecordAssessmentAsync(string learnerId, string certificationId, string questions, string evaluation, CancellationToken ct = default);
    Task RecordNextStepAsync(string learnerId, string recommendation, double readinessScore, CancellationToken ct = default);
}

/// <summary>Read side for the manager dashboard: pending plans + decisions.</summary>
public interface IApprovalQueue
{
    Task<IReadOnlyList<PlanReviewItem>> GetPendingPlansAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PlanReviewItem>> GetRecentPlansAsync(int limit = 20, CancellationToken ct = default);
    Task UpdatePlanDecisionAsync(long id, string decision, string? notes, CancellationToken ct = default);
}
