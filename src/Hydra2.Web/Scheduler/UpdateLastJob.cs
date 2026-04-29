using Hydra2.Downloaders;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace Hydra2.Web.Scheduler;

[DisallowConcurrentExecution]
public class UpdateLastJob : IJob
{
    private readonly IUpdateService _updateService;
    private readonly SchedulerStateTracker _stateTracker;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<UpdateLastJob> _logger;

    public UpdateLastJob(
        IUpdateService updateService,
        SchedulerStateTracker stateTracker,
        IHostApplicationLifetime lifetime,
        ILogger<UpdateLastJob> logger)
    {
        _updateService = updateService;
        _stateTracker = stateTracker;
        _lifetime = lifetime;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("UpdateLastJob Start");
        _stateTracker.MarkLoopStarted();
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                context.CancellationToken,
                _lifetime.ApplicationStopping);

            await _updateService.LastSpotsLoopAsync(linkedCts.Token);
        }
        finally
        {
            _stateTracker.MarkLoopStopped();
            _logger.LogInformation("UpdateLastJob End");
        }
    }
}
