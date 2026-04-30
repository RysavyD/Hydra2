namespace Hydra2.Downloaders;

public interface ISourceStateTracker
{
    void RecordRunStarted(int downLoadType, int stationCount);
    void RecordRunCompleted(int downLoadType, SourceRunOutcome outcome, int ok, int errors, int samplesAdded, string? errorMessage = null);

    SourceState GetState(int downLoadType);
    IReadOnlyList<SourceState> GetAll();
}

public sealed class SourceState
{
    public int DownLoadType { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Running { get; set; }
    public DateTime? LastRunStartedAt { get; set; }
    public DateTime? LastRunCompletedAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public string? LastErrorMessage { get; set; }
    public int LastStationCount { get; set; }
    public int LastOk { get; set; }
    public int LastErrors { get; set; }
    public int LastSamplesAdded { get; set; }
    public int TotalRuns { get; set; }
    public int TotalSuccessRuns { get; set; }

    public TimeSpan? LastDuration =>
        LastRunStartedAt.HasValue && LastRunCompletedAt.HasValue
            ? LastRunCompletedAt - LastRunStartedAt
            : null;

    public TimeSpan? TimeSinceLastSuccess =>
        LastSuccessAt.HasValue ? DateTime.UtcNow - LastSuccessAt.Value : null;
}
