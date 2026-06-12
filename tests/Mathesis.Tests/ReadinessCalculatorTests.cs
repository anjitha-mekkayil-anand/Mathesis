using Mathesis.Core;
using Mathesis.Readiness;

namespace Mathesis.Tests;

public class ReadinessCalculatorTests
{
    private static CertificationConfig Cert(double recommendedHours = 20) => new()
    {
        Id = "AZ-204",
        Name = "Test Cert",
        RecommendedHours = recommendedHours,
        PassThreshold = 75,
        Domains =
        [
            new CertificationDomain { Name = "Compute", Weight = 0.5 },
            new CertificationDomain { Name = "Storage", Weight = 0.3 },
            new CertificationDomain { Name = "Security", Weight = 0.2 }
        ]
    };

    private static LearnerConfig Learner(
        Dictionary<string, int>? ratings = null, double hours = 0, double? practice = null) => new()
    {
        LearnerId = "EMP-T01",
        TargetCertification = "AZ-204",
        StudyHoursLogged = hours,
        PracticeScore = practice,
        DomainRatings = ratings ?? []
    };

    [Fact]
    public void PerfectLearner_ScoresHundred_AndIsReady()
    {
        var learner = Learner(
            new() { ["Compute"] = 5, ["Storage"] = 5, ["Security"] = 5 }, hours: 20, practice: 100);
        var result = new ReadinessCalculator().Assess(learner, Cert());

        Assert.Equal(100, result.Score);
        Assert.Equal(ReadinessBand.Ready, result.Band);
        Assert.Empty(result.GapDomains);
    }

    [Fact]
    public void UnratedDomains_DefaultToLowestConfidence()
    {
        // No ratings at all: every domain counts as 1/5 → domain ratio 20%,
        // contributing 0.2 * 55 = 11 to the total.
        var result = new ReadinessCalculator().Assess(Learner(), Cert());
        Assert.Equal(20, result.DomainComponent);
        Assert.Equal(11, result.Score);
        Assert.Equal(ReadinessBand.NotReady, result.Band);
        Assert.Equal(3, result.GapDomains.Count); // unrated domains are all gaps
    }

    // Score = 55*domainRatio + 25*hoursRatio + 20*practiceRatio
    [Theory]
    [InlineData(true, 0, 100.0, 75, ReadinessBand.Ready)]        // 55+0+20 — exactly 75 is Ready
    [InlineData(true, 0, 99.5, 74.9, ReadinessBand.Borderline)]  // 55+0+19.9 — just under
    [InlineData(false, 15.2, 100.0, 50, ReadinessBand.Borderline)] // 11+19+20 — exactly 50 is Borderline
    [InlineData(false, 15.12, 100.0, 49.9, ReadinessBand.NotReady)] // 11+18.9+20 — just under
    public void BandBoundaries_AreInclusiveAtThresholds(
        bool allRatedFive, double hours, double? practice, double expectedScore, ReadinessBand expectedBand)
    {
        var ratings = allRatedFive
            ? new Dictionary<string, int> { ["Compute"] = 5, ["Storage"] = 5, ["Security"] = 5 }
            : null;
        var result = new ReadinessCalculator().Assess(Learner(ratings, hours, practice), Cert());

        Assert.Equal(expectedScore, result.Score);
        Assert.Equal(expectedBand, result.Band);
    }

    [Fact]
    public void StudyHours_AreCappedAtRecommended()
    {
        var under = new ReadinessCalculator().Assess(Learner(hours: 20), Cert(20));
        var over = new ReadinessCalculator().Assess(Learner(hours: 200), Cert(20));
        Assert.Equal(under.Score, over.Score); // overshooting hours earns nothing extra
        Assert.Equal(100, over.HoursComponent);
    }

    [Fact]
    public void NullPracticeScore_CountsAsZero()
    {
        var result = new ReadinessCalculator().Assess(Learner(practice: null), Cert());
        Assert.Equal(0, result.PracticeComponent);
        Assert.Contains("n/a", result.Detail);
    }

    [Fact]
    public void ZeroRecommendedHours_DoesNotDivideByZero()
    {
        var result = new ReadinessCalculator().Assess(Learner(hours: 0), Cert(recommendedHours: 0));
        Assert.Equal(100, result.HoursComponent); // treated as fully met
    }

    [Fact]
    public void GapDomains_AreRatingsOfThreeOrBelow_OrderedByWeight()
    {
        var learner = Learner(new() { ["Compute"] = 3, ["Storage"] = 2, ["Security"] = 4 });
        var result = new ReadinessCalculator().Assess(learner, Cert());

        Assert.Equal(2, result.GapDomains.Count);
        Assert.Equal("Compute", result.GapDomains[0].Name);  // weight 0.5 first
        Assert.Equal("Storage", result.GapDomains[1].Name);  // weight 0.3 second
        Assert.DoesNotContain(result.GapDomains, d => d.Name == "Security"); // rating 4 is not a gap
    }

    [Fact]
    public void WeightedDomainMath_IsExact()
    {
        // ratings: Compute 5 (0.5), Storage 1 (0.3), Security 1 (0.2)
        // domain = (1.0*0.5 + 0.2*0.3 + 0.2*0.2) = 0.6 → 60% of 55 = 33
        var learner = Learner(new() { ["Compute"] = 5, ["Storage"] = 1, ["Security"] = 1 });
        var result = new ReadinessCalculator().Assess(learner, Cert());
        Assert.Equal(60, result.DomainComponent);
        Assert.Equal(33, result.Score); // 0.6*55 + 0 + 0
    }

    [Fact]
    public void Detail_NamesBandAndGapDomains()
    {
        var learner = Learner(new() { ["Compute"] = 2, ["Storage"] = 5, ["Security"] = 5 }, 10, 60);
        var result = new ReadinessCalculator().Assess(learner, Cert());
        Assert.Contains(result.Band.ToString(), result.Detail);
        Assert.Contains("Compute", result.Detail);
    }
}
