using System.Collections.Concurrent;

namespace Hydra2.Downloaders;

public class SourceStateTracker : ISourceStateTracker
{
    private readonly ConcurrentDictionary<int, SourceState> _states;

    public SourceStateTracker()
    {
        _states = new ConcurrentDictionary<int, SourceState>(
            SourceCatalog.All.ToDictionary(
                s => s.DownLoadType,
                s => new SourceState { DownLoadType = s.DownLoadType, Name = s.Name }));
    }

    public void RecordRunStarted(int downLoadType, int stationCount)
    {
        var state = GetOrCreate(downLoadType);
        lock (state)
        {
            state.Running = true;
            state.LastRunStartedAt = DateTime.UtcNow;
            state.LastStationCount = stationCount;
            state.TotalRuns++;
        }
    }

    public void RecordRunCompleted(int downLoadType, SourceRunOutcome outcome, int ok, int errors, int samplesAdded, string? errorMessage = null)
    {
        var state = GetOrCreate(downLoadType);
        var now = DateTime.UtcNow;
        lock (state)
        {
            state.Running = false;
            state.LastRunCompletedAt = now;
            state.LastOk = ok;
            state.LastErrors = errors;
            state.LastSamplesAdded = samplesAdded;

            if (outcome == SourceRunOutcome.Success || outcome == SourceRunOutcome.PartialFailure)
            {
                state.LastSuccessAt = now;
                state.TotalSuccessRuns++;
            }

            if (outcome == SourceRunOutcome.Failure || outcome == SourceRunOutcome.PartialFailure)
            {
                state.LastErrorAt = now;
                state.LastErrorMessage = errorMessage;
            }
        }
    }

    public SourceState GetState(int downLoadType) => GetOrCreate(downLoadType);

    public IReadOnlyList<SourceState> GetAll() => _states.Values.OrderBy(s => s.DownLoadType).ToList();

    private SourceState GetOrCreate(int downLoadType) =>
        _states.GetOrAdd(downLoadType, t => new SourceState
        {
            DownLoadType = t,
            Name = SourceCatalog.NameFor(t),
        });
}
