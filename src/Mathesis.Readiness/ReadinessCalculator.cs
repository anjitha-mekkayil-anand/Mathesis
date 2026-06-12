using Mathesis.Core;

namespace Mathesis.Readiness;

/// <summary>
/// Deterministic readiness scoring — the cheap pre-filter. The LLM agents only
/// reason when this calculator says a learner needs a plan; a Ready learner
/// short-circuits to a booking recommendation without burning agent calls.
///
/// Formula (weights follow the established pattern for certification readiness):
///   55% — weighted domain self-ratings vs certification domain weights
///   25% — study hours logged vs recommended hours
///   20% — latest practice score vs 100
/// </summary>
public sealed class ReadinessCalculator
{
    private const double DomainWeight = 0.55;
    private const double HoursWeight = 0.25;
    private const double PracticeWeight = 0.20;

    public ReadinessAssessment Assess(LearnerConfig learner, CertificationConfig certification)
    {
        // Domain component: weighted average of self-ratings (1–5) across the
        // certification's domains. Unrated domains count as 1 (lowest confidence).
        double domainScore = 0;
        var gapDomains = new List<CertificationDomain>();
        foreach (var domain in certification.Domains)
        {
            var rating = learner.DomainRatings.GetValueOrDefault(domain.Name, 1);
            domainScore += (rating / 5.0) * domain.Weight;
            if (rating <= 3)
                gapDomains.Add(domain);
        }

        var hoursScore = certification.RecommendedHours <= 0
            ? 1.0
            : Math.Min(1.0, learner.StudyHoursLogged / certification.RecommendedHours);

        var practiceScore = (learner.PracticeScore ?? 0) / 100.0;

        var total = Math.Round(
            100 * (domainScore * DomainWeight + hoursScore * HoursWeight + practiceScore * PracticeWeight), 1);

        var band = total switch
        {
            >= 75 => ReadinessBand.Ready,
            >= 50 => ReadinessBand.Borderline,
            _ => ReadinessBand.NotReady
        };

        var gapSummary = gapDomains.Count == 0
            ? "no weak domains"
            : $"weak domains: {string.Join(", ", gapDomains.OrderByDescending(d => d.Weight).Select(d => d.Name))}";

        return new ReadinessAssessment(
            learner.LearnerId,
            certification.Id,
            total,
            band,
            Math.Round(domainScore * 100, 1),
            Math.Round(hoursScore * 100, 1),
            Math.Round(practiceScore * 100, 1),
            gapDomains.OrderByDescending(d => d.Weight).ToList(),
            $"{total}/100 ({band}) — domains {Math.Round(domainScore * 100)}%, " +
            $"hours {learner.StudyHoursLogged}/{certification.RecommendedHours}, " +
            $"practice {learner.PracticeScore?.ToString("0") ?? "n/a"}; {gapSummary}");
    }
}
