using System.Reflection;
using Hydra2.Downloaders;
using Hydra2.Service;
using Hydra2.Web.Scheduler;
using Microsoft.AspNetCore.Mvc;

namespace Hydra2.Web.Controllers;

[ApiController]
[Route("api/heartbeat")]
public class HeartbeatController : ControllerBase
{
    private static readonly string AssemblyVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

    private readonly SchedulerStateTracker _stateTracker;
    private readonly IConfigService _configService;
    private readonly ICycleStats _cycleStats;
    private readonly ILogger<HeartbeatController> _logger;

    public HeartbeatController(
        SchedulerStateTracker stateTracker,
        IConfigService configService,
        ICycleStats cycleStats,
        ILogger<HeartbeatController> logger)
    {
        _stateTracker = stateTracker;
        _configService = configService;
        _cycleStats = cycleStats;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var processStarted = _stateTracker.ProcessStarted;
        var loopStarted = _stateTracker.LoopStarted;
        var lastIteration = _stateTracker.LastIteration;

        int? currentStationId = null;
        try
        {
            var config = await _configService.GetFirstConfigAsync(cancellationToken);
            currentStationId = config.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat: failed to read config");
        }

        var lastCycle = _cycleStats.GetLastCompleted();
        var current = _cycleStats.GetCurrent();

        var status = ResolveStatus(loopStarted, lastIteration, currentStationId.HasValue);

        return Ok(new
        {
            now,
            uptime = (now - processStarted).ToString(@"d\.hh\:mm\:ss"),
            version = AssemblyVersion,
            scheduler = new
            {
                running = _stateTracker.IsLoopRunning,
                loopStarted,
                lastIteration,
                currentStationId,
            },
            cycle = new
            {
                inProgress = new
                {
                    startedAt = current.StartedAt,
                    attempts = current.Attempts,
                    ok = current.Ok,
                    skipped = current.Skipped,
                    errors = current.Errors,
                    samplesAdded = current.SamplesAdded,
                },
                lastCompleted = lastCycle is null ? null : new
                {
                    startedAt = lastCycle.StartedAt,
                    completedAt = lastCycle.CompletedAt,
                    duration = lastCycle.Duration.ToString(@"hh\:mm\:ss"),
                    attempts = lastCycle.Attempts,
                    ok = lastCycle.Ok,
                    skipped = lastCycle.Skipped,
                    errors = lastCycle.Errors,
                    samplesAdded = lastCycle.SamplesAdded,
                },
            },
            status,
        });
    }

    private static string ResolveStatus(DateTime? loopStarted, DateTime? lastIteration, bool configReadable)
    {
        if (!configReadable) return "DEGRADED";
        if (loopStarted is null) return "STARTING";
        if (lastIteration is null) return "STARTING";

        var stale = DateTime.UtcNow - lastIteration.Value > TimeSpan.FromMinutes(15);
        return stale ? "STALE" : "OK";
    }
}
