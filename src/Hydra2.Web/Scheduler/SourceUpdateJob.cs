using Hydra2.Downloaders;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace Hydra2.Web.Scheduler;

[DisallowConcurrentExecution]
public class SourceUpdateJob : IJob
{
    public const string DownLoadTypeKey = "downLoadType";

    private readonly IUpdateService _updateService;
    private readonly SchedulerStateTracker _stateTracker;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<SourceUpdateJob> _logger;

    public SourceUpdateJob(
        IUpdateService updateService,
        SchedulerStateTracker stateTracker,
        IHostApplicationLifetime lifetime,
        ILogger<SourceUpdateJob> logger)
    {
        _updateService = updateService;
        _stateTracker = stateTracker;
        _lifetime = lifetime;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (!context.JobDetail.JobDataMap.TryGetIntValue(DownLoadTypeKey, out var downLoadType))
        {
            _logger.LogError("SourceUpdateJob {Key} missing JobDataMap entry '{DataKey}'",
                context.JobDetail.Key, DownLoadTypeKey);
            return;
        }

        _stateTracker.MarkLoopStarted();
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                context.CancellationToken,
                _lifetime.ApplicationStopping);

            await _updateService.UpdateSourceAsync(downLoadType, linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SourceUpdateJob {Key} failed unexpectedly",
                context.JobDetail.Key);
        }
        finally
        {
            _stateTracker.MarkLoopStopped();
        }
    }
}
