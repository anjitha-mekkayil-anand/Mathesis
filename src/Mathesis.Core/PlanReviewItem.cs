namespace Mathesis.Core;

/// <summary>
/// One study plan awaiting (or past) manager review — the human approval gate.
/// A plan never becomes active without a manager decision.
/// </summary>
public sealed record PlanReviewItem(
    long Id,
    string LearnerId,
    string CertificationId,
    string Plan,
    string Rationale,
    DateTimeOffset CreatedAt,
    string Status,
    string? RecommendedNextStep,
    string? Notes,
    DateTimeOffset? DecidedAt);
