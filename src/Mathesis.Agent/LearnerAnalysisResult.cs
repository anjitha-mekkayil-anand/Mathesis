namespace Mathesis.Agent;

/// <summary>What one full pipeline pass over a learner produced.</summary>
public sealed record LearnerAnalysisResult(
    string LearnerId,
    string CertificationId,
    double ReadinessScore,
    string Band,
    string? CuratedPath,
    bool PlanProposed,
    string? AssessmentSummary,
    string NextStep,
    DateTimeOffset Timestamp);
