using Hydra2.Downloaders;
using Microsoft.Extensions.Time.Testing;

namespace Hydra2.Tests.Downloaders;

public class StationErrorTrackerTests
{
    [Fact]
    public void First_error_for_pair_returns_true()
    {
        var tracker = new StationErrorTracker();
        var ex = new InvalidOperationException("boom");

        tracker.RecordError(stationId: 1, ex).Should().BeTrue();
    }

    [Fact]
    public void Repeated_error_within_throttle_window_returns_false()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var tracker = new StationErrorTracker(TimeSpan.FromHours(1), time);
        var ex = new InvalidOperationException();

        tracker.RecordError(1, ex).Should().BeTrue();
        time.Advance(TimeSpan.FromMinutes(30));
        tracker.RecordError(1, ex).Should().BeFalse();
    }

    [Fact]
    public void Error_after_throttle_window_returns_true_again()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var tracker = new StationErrorTracker(TimeSpan.FromHours(1), time);
        var ex = new InvalidOperationException();

        tracker.RecordError(1, ex).Should().BeTrue();
        time.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));
        tracker.RecordError(1, ex).Should().BeTrue();
    }

    [Fact]
    public void Different_exception_types_have_independent_throttle()
    {
        var tracker = new StationErrorTracker();

        tracker.RecordError(1, new InvalidOperationException()).Should().BeTrue();
        tracker.RecordError(1, new HttpRequestException()).Should().BeTrue();
    }

    [Fact]
    public void Different_stations_have_independent_throttle()
    {
        var tracker = new StationErrorTracker();
        var ex = new InvalidOperationException();

        tracker.RecordError(1, ex).Should().BeTrue();
        tracker.RecordError(2, ex).Should().BeTrue();
    }

    [Fact]
    public void GetReport_returns_only_stations_with_errors_sorted_by_count()
    {
        var tracker = new StationErrorTracker();
        for (var i = 0; i < 3; i++) tracker.RecordError(1, new InvalidOperationException());
        for (var i = 0; i < 5; i++) tracker.RecordError(2, new InvalidOperationException());
        tracker.RecordSuccess(3); // station 3 only has success - excluded

        var report = tracker.GetReport();
        report.Should().HaveCount(2);
        report.Select(r => r.StationId).Should().ContainInOrder(2, 1);
    }

    [Fact]
    public void GetReport_topN_limits_results()
    {
        var tracker = new StationErrorTracker();
        for (var i = 1; i <= 5; i++)
            tracker.RecordError(i, new InvalidOperationException());

        tracker.GetReport(topN: 2).Should().HaveCount(2);
    }

    [Fact]
    public void Report_includes_errors_by_type()
    {
        var tracker = new StationErrorTracker();
        tracker.RecordError(1, new InvalidOperationException());
        tracker.RecordError(1, new InvalidOperationException());
        tracker.RecordError(1, new HttpRequestException());

        var report = tracker.GetReport().Single();
        report.ErrorsByType.Should().Contain("InvalidOperationException", 2);
        report.ErrorsByType.Should().Contain("HttpRequestException", 1);
    }

    [Fact]
    public void ErrorRate_reflects_success_count()
    {
        var tracker = new StationErrorTracker();
        tracker.RecordError(1, new InvalidOperationException());
        tracker.RecordSuccess(1);
        tracker.RecordSuccess(1);
        tracker.RecordSuccess(1);

        var report = tracker.GetReport().Single();
        report.ErrorRate.Should().BeApproximately(0.25, 0.001);
    }
}
