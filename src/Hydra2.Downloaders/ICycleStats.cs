namespace Hydra2.Downloaders;

public interface ICycleStats
{
    void RecordIteration(int stationId, IterationOutcome outcome, int samplesAdded);

    /// <summary>
    /// Marks the boundary of a cycle (called when station counter wraps to 0).
    /// Returns the snapshot of the cycle that just finished, or null if there was no previous cycle.
    /// </summary>
    CycleStatsSnapshot? CompleteCycle();

    CycleStatsSnapshot GetCurrent();
    CycleStatsSnapshot? GetLastCompleted();
}

public sealed record CycleStatsSnapshot(
    DateTime StartedAt,
    DateTime? CompletedAt,
    int Attempts,
    int Ok,
    int Skipped,
    int Errors,
    int SamplesAdded)
{
    public TimeSpan Duration => (CompletedAt ?? DateTime.UtcNow) - StartedAt;
}
