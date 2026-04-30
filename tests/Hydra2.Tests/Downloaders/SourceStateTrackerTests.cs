using Hydra2.Downloaders;

namespace Hydra2.Tests.Downloaders;

public class SourceStateTrackerTests
{
    [Fact]
    public void Initially_all_known_sources_have_empty_state()
    {
        var tracker = new SourceStateTracker();
        var all = tracker.GetAll();

        all.Should().HaveCount(SourceCatalog.All.Count);
        all.Should().OnlyContain(s => s.LastSuccessAt == null && s.LastErrorAt == null && !s.Running);
    }

    [Fact]
    public void RecordRunStarted_sets_running_and_increments_TotalRuns()
    {
        var tracker = new SourceStateTracker();
        tracker.RecordRunStarted(downLoadType: 1, stationCount: 145);

        var s = tracker.GetState(1);
        s.Running.Should().BeTrue();
        s.LastStationCount.Should().Be(145);
        s.TotalRuns.Should().Be(1);
        s.LastRunStartedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordRunCompleted_with_Success_updates_LastSuccess_only()
    {
        var tracker = new SourceStateTracker();
        tracker.RecordRunStarted(1, 10);
        tracker.RecordRunCompleted(1, SourceRunOutcome.Success, ok: 10, errors: 0, samplesAdded: 50);

        var s = tracker.GetState(1);
        s.Running.Should().BeFalse();
        s.LastSuccessAt.Should().NotBeNull();
        s.LastErrorAt.Should().BeNull();
        s.LastErrorMessage.Should().BeNull();
        s.LastOk.Should().Be(10);
        s.LastErrors.Should().Be(0);
        s.LastSamplesAdded.Should().Be(50);
        s.TotalSuccessRuns.Should().Be(1);
    }

    [Fact]
    public void RecordRunCompleted_with_Failure_sets_error_and_keeps_LastSuccessAt_null()
    {
        var tracker = new SourceStateTracker();
        tracker.RecordRunStarted(1, 10);
        tracker.RecordRunCompleted(1, SourceRunOutcome.Failure, ok: 0, errors: 10, samplesAdded: 0,
            errorMessage: "boom");

        var s = tracker.GetState(1);
        s.LastSuccessAt.Should().BeNull();
        s.LastErrorAt.Should().NotBeNull();
        s.LastErrorMessage.Should().Be("boom");
        s.TotalSuccessRuns.Should().Be(0);
    }

    [Fact]
    public void RecordRunCompleted_with_PartialFailure_sets_both_success_and_error()
    {
        var tracker = new SourceStateTracker();
        tracker.RecordRunStarted(1, 10);
        tracker.RecordRunCompleted(1, SourceRunOutcome.PartialFailure, ok: 8, errors: 2, samplesAdded: 16,
            errorMessage: "two stations failed");

        var s = tracker.GetState(1);
        s.LastSuccessAt.Should().NotBeNull();
        s.LastErrorAt.Should().NotBeNull();
        s.LastErrorMessage.Should().Be("two stations failed");
        s.TotalSuccessRuns.Should().Be(1);
    }

    [Fact]
    public void GetAll_returns_states_ordered_by_DownLoadType()
    {
        var tracker = new SourceStateTracker();
        var ordered = tracker.GetAll().Select(s => s.DownLoadType).ToArray();
        ordered.Should().BeInAscendingOrder();
    }

    [Fact]
    public void TimeSinceLastSuccess_is_positive_after_success()
    {
        var tracker = new SourceStateTracker();
        tracker.RecordRunStarted(1, 1);
        tracker.RecordRunCompleted(1, SourceRunOutcome.Success, 1, 0, 1);

        Thread.Sleep(10);
        tracker.GetState(1).TimeSinceLastSuccess!.Value.TotalMilliseconds.Should().BeGreaterThan(0);
    }
}
