namespace Hydra2.Web.Scheduler;

public class SchedulerStateTracker
{
    private long _loopStartedTicks;
    private long _loopRunning;
    private long _lastIterationTicks;

    public DateTime ProcessStarted { get; } = DateTime.UtcNow;

    public DateTime? LoopStarted
    {
        get
        {
            var ticks = Interlocked.Read(ref _loopStartedTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    public DateTime? LastIteration
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastIterationTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    public bool IsLoopRunning => Interlocked.Read(ref _loopRunning) == 1;

    public void MarkLoopStarted()
    {
        Interlocked.Exchange(ref _loopStartedTicks, DateTime.UtcNow.Ticks);
        Interlocked.Exchange(ref _loopRunning, 1);
    }

    public void MarkLoopStopped() => Interlocked.Exchange(ref _loopRunning, 0);

    public void MarkIteration() => Interlocked.Exchange(ref _lastIterationTicks, DateTime.UtcNow.Ticks);
}
