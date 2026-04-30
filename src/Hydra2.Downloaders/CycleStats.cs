namespace Hydra2.Downloaders;

public class CycleStats : ICycleStats
{
    private readonly object _gate = new();
    private CycleStatsSnapshot _current = new(DateTime.UtcNow, null, 0, 0, 0, 0, 0);
    private CycleStatsSnapshot? _lastCompleted;

    public void RecordIteration(int stationId, IterationOutcome outcome, int samplesAdded)
    {
        lock (_gate)
        {
            _current = _current with
            {
                Attempts = _current.Attempts + 1,
                Ok = _current.Ok + (outcome == IterationOutcome.Ok ? 1 : 0),
                Skipped = _current.Skipped + (outcome == IterationOutcome.Skipped ? 1 : 0),
                Errors = _current.Errors + (outcome == IterationOutcome.Error ? 1 : 0),
                SamplesAdded = _current.SamplesAdded + samplesAdded,
            };
        }
    }

    public CycleStatsSnapshot? CompleteCycle()
    {
        lock (_gate)
        {
            if (_current.Attempts == 0) return null;

            var completed = _current with { CompletedAt = DateTime.UtcNow };
            _lastCompleted = completed;
            _current = new CycleStatsSnapshot(DateTime.UtcNow, null, 0, 0, 0, 0, 0);
            return completed;
        }
    }

    public CycleStatsSnapshot GetCurrent()
    {
        lock (_gate) return _current;
    }

    public CycleStatsSnapshot? GetLastCompleted()
    {
        lock (_gate) return _lastCompleted;
    }
}
