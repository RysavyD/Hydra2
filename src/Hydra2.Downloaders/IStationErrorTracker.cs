namespace Hydra2.Downloaders;

public interface IStationErrorTracker
{
    /// <summary>
    /// Records an error for a station. Returns true if the caller should log a full stack trace
    /// (first occurrence of this (stationId, exceptionType) pair within the throttle window),
    /// false if a compact one-line log is sufficient.
    /// </summary>
    bool RecordError(int stationId, Exception exception);

    void RecordSuccess(int stationId);

    IReadOnlyCollection<StationErrorReport> GetReport(int? topN = null);
}

public sealed record StationErrorReport(
    int StationId,
    int ErrorCount,
    int SuccessCount,
    DateTime FirstError,
    DateTime LastError,
    IReadOnlyDictionary<string, int> ErrorsByType)
{
    public double ErrorRate => (ErrorCount + SuccessCount) == 0
        ? 0
        : (double)ErrorCount / (ErrorCount + SuccessCount);
}
