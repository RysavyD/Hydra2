using System.Collections.Concurrent;

namespace Hydra2.Downloaders;

public class StationErrorTracker : IStationErrorTracker
{
    private readonly TimeSpan _stackTraceThrottle;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<int, StationStats> _stations = new();
    private readonly ConcurrentDictionary<(int StationId, string ExceptionType), DateTime> _lastStackLog = new();

    public StationErrorTracker() : this(TimeSpan.FromHours(1), TimeProvider.System) { }

    public StationErrorTracker(TimeSpan stackTraceThrottle, TimeProvider timeProvider)
    {
        _stackTraceThrottle = stackTraceThrottle;
        _timeProvider = timeProvider;
    }

    public bool RecordError(int stationId, Exception exception)
    {
        var exType = exception.GetType().Name;

        var stats = _stations.GetOrAdd(stationId, _ => new StationStats());
        stats.RecordError(exType, _timeProvider.GetUtcNow().UtcDateTime);

        var key = (stationId, exType);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var shouldLogStack = false;

        _lastStackLog.AddOrUpdate(key,
            _ =>
            {
                shouldLogStack = true;
                return now;
            },
            (_, last) =>
            {
                if (now - last >= _stackTraceThrottle)
                {
                    shouldLogStack = true;
                    return now;
                }
                return last;
            });

        return shouldLogStack;
    }

    public void RecordSuccess(int stationId)
    {
        var stats = _stations.GetOrAdd(stationId, _ => new StationStats());
        stats.RecordSuccess();
    }

    public IReadOnlyCollection<StationErrorReport> GetReport(int? topN = null)
    {
        IEnumerable<StationErrorReport> query = _stations
            .Where(kv => kv.Value.ErrorCount > 0)
            .Select(kv => kv.Value.ToReport(kv.Key))
            .OrderByDescending(r => r.ErrorCount)
            .ThenByDescending(r => r.ErrorRate);

        if (topN.HasValue) query = query.Take(topN.Value);

        return query.ToArray();
    }

    private sealed class StationStats
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, int> _errorsByType = new();
        public int ErrorCount { get; private set; }
        public int SuccessCount { get; private set; }
        public DateTime FirstError { get; private set; }
        public DateTime LastError { get; private set; }

        public void RecordError(string exceptionType, DateTime now)
        {
            lock (_gate)
            {
                ErrorCount++;
                LastError = now;
                if (FirstError == default) FirstError = LastError;
                _errorsByType[exceptionType] = _errorsByType.GetValueOrDefault(exceptionType) + 1;
            }
        }

        public void RecordSuccess()
        {
            lock (_gate) SuccessCount++;
        }

        public StationErrorReport ToReport(int stationId)
        {
            lock (_gate)
            {
                return new StationErrorReport(
                    stationId,
                    ErrorCount,
                    SuccessCount,
                    FirstError,
                    LastError,
                    new Dictionary<string, int>(_errorsByType));
            }
        }
    }
}
