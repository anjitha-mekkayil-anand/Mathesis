using Mathesis.Core;

namespace Mathesis.Readiness;

public enum ReadinessBand
{
    NotReady,
    Borderline,
    Ready
}

/// <summary>The calculator's deterministic read on one learner vs one certification.</summary>
public sealed record ReadinessAssessment(
    string LearnerId,
    string CertificationId,
    double Score,
    ReadinessBand Band,
    double DomainComponent,
    double HoursComponent,
    double PracticeComponent,
    IReadOnlyList<CertificationDomain> GapDomains,
    string Detail);
