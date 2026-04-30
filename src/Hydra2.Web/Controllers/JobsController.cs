using Hydra2.Downloaders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Quartz;

namespace Hydra2.Web.Controllers;

[ApiController]
[Route("api/jobs")]
public class JobsController : ControllerBase
{
    private readonly IUpdateService _updateService;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IUpdateService updateService,
        ISchedulerFactory schedulerFactory,
        IOptions<AuthOptions> authOptions,
        ILogger<JobsController> logger)
    {
        _updateService = updateService;
        _schedulerFactory = schedulerFactory;
        _authOptions = authOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Manually triggers a single source run (in-process). Returns when the run completes.
    /// </summary>
    [HttpPost("trigger/{name}")]
    public async Task<IActionResult> Trigger(string name, string? token, CancellationToken cancellationToken)
    {
        if (!IsAuthorized(token)) return Unauthorized();

        var source = SourceCatalog.FindByName(name);
        if (source is null) return NotFound(new { error = $"Unknown source '{name}'", available = SourceCatalog.All.Select(s => s.Name) });

        _logger.LogInformation("Manual trigger for source {Source} (downLoadType {Type})", source.Name, source.DownLoadType);
        var outcome = await _updateService.UpdateSourceAsync(source.DownLoadType, cancellationToken);

        return Ok(new { source = source.Name, outcome = outcome.ToString() });
    }

    /// <summary>
    /// Schedule the source job to fire at the next opportunity (does not block on completion).
    /// Useful when you want fire-and-forget without waiting for the run.
    /// </summary>
    [HttpPost("schedule/{name}")]
    public async Task<IActionResult> ScheduleNow(string name, string? token, CancellationToken cancellationToken)
    {
        if (!IsAuthorized(token)) return Unauthorized();

        var source = SourceCatalog.FindByName(name);
        if (source is null) return NotFound(new { error = $"Unknown source '{name}'" });

        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey($"source-{source.Name}", "sources");

        if (!await scheduler.CheckExists(jobKey, cancellationToken))
            return NotFound(new { error = $"Job for source '{source.Name}' is not registered" });

        await scheduler.TriggerJob(jobKey, cancellationToken);
        _logger.LogInformation("Source {Source} scheduled to run now", source.Name);
        return Accepted(new { source = source.Name, scheduled = true });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(string? token, CancellationToken cancellationToken)
    {
        if (!IsAuthorized(token)) return Unauthorized();

        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var triggers = await scheduler.GetTriggerKeys(
            Quartz.Impl.Matchers.GroupMatcher<TriggerKey>.GroupEquals("sources"), cancellationToken);

        var rows = new List<object>();
        foreach (var key in triggers)
        {
            var trigger = await scheduler.GetTrigger(key, cancellationToken);
            if (trigger is null) continue;
            rows.Add(new
            {
                trigger = key.Name,
                job = trigger.JobKey.Name,
                state = (await scheduler.GetTriggerState(key, cancellationToken)).ToString(),
                nextFireUtc = trigger.GetNextFireTimeUtc()?.UtcDateTime,
                previousFireUtc = trigger.GetPreviousFireTimeUtc()?.UtcDateTime,
                cron = (trigger as ICronTrigger)?.CronExpressionString,
            });
        }

        return Ok(rows);
    }

    private bool IsAuthorized(string? token) =>
        !string.IsNullOrEmpty(_authOptions.SecretToken) &&
        token == _authOptions.SecretToken;
}
