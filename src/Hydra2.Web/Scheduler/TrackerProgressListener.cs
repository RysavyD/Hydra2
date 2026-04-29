using Hydra2.Downloaders;

namespace Hydra2.Web.Scheduler;

public class TrackerProgressListener : IUpdateProgressListener
{
    private readonly SchedulerStateTracker _stateTracker;

    public TrackerProgressListener(SchedulerStateTracker stateTracker)
    {
        _stateTracker = stateTracker;
    }

    public void OnIterationStarted(int stationId)
    {
        _stateTracker.MarkIteration();
    }

    public void OnIterationCompleted(int stationId)
    {
        _stateTracker.MarkIteration();
    }
}
