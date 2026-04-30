using System.Reflection;
using Hydra2.Downloaders;
using Hydra2.Service;
using Hydra2.Web.Scheduler;
using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace Hydra2.Web.Controllers;

[ApiController]
[Route("api/heartbeat")]
public class HeartbeatController : ControllerBase
{
    private static readonly TimeSpan StaleSourceThreshold = TimeSpan.FromMinutes(60);

    private static readonly string AssemblyVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

    private readonly SchedulerStateTracker _stateTracker;
    private readonly IConfigService _configService;
    private readonly ISourceStateTracker _sourceStateTracker;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<HeartbeatController> _logger;

    public HeartbeatController(
        SchedulerStateTracker stateTracker,
        IConfigService configService,
        ISourceStateTracker sourceStateTracker,
        ISchedulerFactory schedulerFactory,
        ILogger<HeartbeatController> logger)
    {
        _stateTracker = stateTracker;
        _configService = configService;
        _sourceStateTracker = sourceStateTracker;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var processStarted = _stateTracker.ProcessStarted;

        var dbHealthy = true;
        try
        {
            await _configService.GetFirstConfigAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            dbHealthy = false;
            _logger.LogWarning(ex, "Heartbeat: failed to read config");
        }

        var sources = _sourceStateTracker.GetAll();
        var nextFireTimes = await GetNextFireTimesAsync(cancellationToken);

        var sourcePayload = sources.Select(s => new
        {
            name = s.Name,
            downLoadType = s.DownLoadType,
            running = s.Running,
            lastRunStartedAt = s.LastRunStartedAt,
            lastRunCompletedAt = s.LastRunCompletedAt,
            lastSuccessAt = s.LastSuccessAt,
            lastErrorAt = s.LastErrorAt,
            lastErrorMessage = s.LastErrorMessage,
            lastDuration = s.LastDuration?.ToString(@"hh\:mm\:ss"),
            stationsTotal = s.LastStationCount,
            stationsOk = s.LastOk,
            stationsErrors = s.LastErrors,
            samplesAdded = s.LastSamplesAdded,
            totalRuns = s.TotalRuns,
            totalSuccessRuns = s.TotalSuccessRuns,
            minutesSinceSuccess = s.TimeSinceLastSuccess.HasValue
                ? Math.Round(s.TimeSinceLastSuccess.Value.TotalMinutes, 1)
                : (double?)null,
            nextScheduledAt = nextFireTimes.GetValueOrDefault(s.Name),
        }).ToArray();

        var status = ResolveStatus(dbHealthy, sources);

        var response = new
        {
            now,
            uptime = (now - processStarted).ToString(@"d\.hh\:mm\:ss"),
            version = AssemblyVersion,
            db = dbHealthy ? "OK" : "ERROR",
            sources = sourcePayload,
            status,
        };

        if (status == "STALE" || status == "DEGRADED")
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        return Ok(response);
    }

    private async Task<Dictionary<string, DateTime?>> GetNextFireTimesAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(ct);
            var triggers = await scheduler.GetTriggerKeys(
                Quartz.Impl.Matchers.GroupMatcher<TriggerKey>.GroupEquals("sources"), ct);

            foreach (var key in triggers)
            {
                var trigger = await scheduler.GetTrigger(key, ct);
                if (trigger?.JobKey?.Name is { } jobName && jobName.StartsWith("source-"))
                {
                    var sourceName = jobName["source-".Length..];
                    result[sourceName] = trigger.GetNextFireTimeUtc()?.UtcDateTime;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Quartz next fire times");
        }
        return result;
    }

    private static string ResolveStatus(bool dbHealthy, IReadOnlyList<SourceState> sources)
    {
        if (!dbHealthy) return "DEGRADED";

        var anySourceRan = sources.Any(s => s.LastSuccessAt.HasValue);
        if (!anySourceRan) return "STARTING";

        var staleSource = sources.FirstOrDefault(s =>
            s.LastSuccessAt.HasValue &&
            DateTime.UtcNow - s.LastSuccessAt.Value > StaleSourceThreshold);
        return staleSource is null ? "OK" : "STALE";
    }
}
