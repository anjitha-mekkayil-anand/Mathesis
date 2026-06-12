using Mathesis.Data;

namespace Mathesis.Tests;

public class SqliteLearningStoreTests : IAsyncLifetime
{
    private SqliteLearningStore _store = null!;

    public async Task InitializeAsync()
    {
        _store = new SqliteLearningStore("Data Source=:memory:");
        await _store.InitializeAsync();
    }

    public async Task DisposeAsync() => await _store.DisposeAsync();

    [Fact]
    public async Task StudyPlan_RoundTrips_AsPending()
    {
        await _store.RecordStudyPlanAsync("EMP-T01", "AZ-204", "week 1: compute", "fits focus hours");
        var pending = await _store.GetPendingPlansAsync();

        var plan = Assert.Single(pending);
        Assert.Equal("EMP-T01", plan.LearnerId);
        Assert.Equal("pending", plan.Status);
        Assert.Null(plan.DecidedAt);
    }

    [Fact]
    public async Task Approval_MovesPlanOutOfPending_AndRecordsDecision()
    {
        await _store.RecordStudyPlanAsync("EMP-T01", "AZ-204", "plan", "rationale");
        var id = (await _store.GetPendingPlansAsync())[0].Id;

        await _store.UpdatePlanDecisionAsync(id, "approved", "looks right");

        Assert.Empty(await _store.GetPendingPlansAsync());
        var decided = (await _store.GetRecentPlansAsync())[0];
        Assert.Equal("approved", decided.Status);
        Assert.Equal("looks right", decided.Notes);
        Assert.NotNull(decided.DecidedAt);
    }

    [Fact]
    public async Task ReadinessSnapshots_ReturnNewestFirst_AndHonorLimit()
    {
        var t0 = DateTimeOffset.UtcNow.AddDays(-14);
        await _store.RecordReadinessSnapshotAsync("EMP-T01", "AZ-204", 40, "NotReady", t0);
        await _store.RecordReadinessSnapshotAsync("EMP-T01", "AZ-204", 55, "Borderline", t0.AddDays(7));
        await _store.RecordReadinessSnapshotAsync("EMP-T01", "AZ-204", 70, "Borderline", t0.AddDays(14));

        var latestTwo = await _store.GetReadinessSnapshotsAsync("EMP-T01", limit: 2);

        Assert.Equal(2, latestTwo.Count);
        Assert.Equal(70, latestTwo[0].Score); // newest first
        Assert.Equal(55, latestTwo[1].Score);
    }

    [Fact]
    public async Task Snapshots_AreIsolatedPerLearner()
    {
        await _store.RecordReadinessSnapshotAsync("EMP-A", "AZ-204", 50, "Borderline");
        await _store.RecordReadinessSnapshotAsync("EMP-B", "AZ-400", 80, "Ready");

        var a = await _store.GetReadinessSnapshotsAsync("EMP-A");
        Assert.Single(a);
        Assert.Equal(50, a[0].Score);
    }

    [Fact]
    public async Task NextStep_JoinsOntoLatestPlan()
    {
        await _store.RecordStudyPlanAsync("EMP-T01", "AZ-204", "plan", "rationale");
        await _store.RecordNextStepAsync("EMP-T01", "extend the plan by 2 weeks", 66.6);

        var plan = (await _store.GetPendingPlansAsync())[0];
        Assert.Equal("extend the plan by 2 weeks", plan.RecommendedNextStep);
    }
}
