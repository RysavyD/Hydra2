using System.Globalization;
using Hydra2.Service;
using Microsoft.Extensions.Logging;

namespace Hydra2.Downloaders;

public class UpdateService : IUpdateService
{
    private const int MaxStationId = 650;

    private readonly IDataService _dataService;
    private readonly IConfigService _configService;
    private readonly IDownloaderFactory _downloaderFactory;
    private readonly IUpdateProgressListener _progressListener;
    private readonly ICycleStats _cycleStats;
    private readonly IStationErrorTracker _errorTracker;
    private readonly ILogger<UpdateService> _logger;

    public UpdateService(
        IDataService dataService,
        IConfigService configService,
        IDownloaderFactory downloaderFactory,
        IUpdateProgressListener progressListener,
        ICycleStats cycleStats,
        IStationErrorTracker errorTracker,
        ILogger<UpdateService> logger)
    {
        _dataService = dataService;
        _configService = configService;
        _downloaderFactory = downloaderFactory;
        _progressListener = progressListener;
        _cycleStats = cycleStats;
        _errorTracker = errorTracker;
        _logger = logger;
    }

    public async Task UpdateSpotsAsync(int startIndex, int stopIndex, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual update {Start}-{Stop} started", startIndex, stopIndex);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");

        for (var i = startIndex; i <= stopIndex; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateSingleStationAsync(i, cancellationToken);
        }

        _logger.LogInformation("Manual update {Start}-{Stop} finished", startIndex, stopIndex);
    }

    public async Task LastSpotsLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Update loop started");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await UpdateNextSpotAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in update loop iteration");
            }
        }

        _logger.LogInformation("Update loop stopped");
    }

    public async Task UpdateNextSpotAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configService.GetFirstConfigAsync(cancellationToken);

        var nextValue = config.Value + 1;
        var cycleWrapped = false;
        if (nextValue > MaxStationId)
        {
            nextValue = 0;
            cycleWrapped = true;
        }

        await _configService.UpdateConfigAsync(config.Id, nextValue, cancellationToken);

        if (cycleWrapped)
        {
            EmitCycleSummary();
        }

        await UpdateSingleStationAsync(nextValue, cancellationToken);
    }

    private void EmitCycleSummary()
    {
        var snapshot = _cycleStats.CompleteCycle();
        if (snapshot is null) return;

        _logger.LogInformation(
            "Cycle complete: {Attempts} attempts, {Ok} ok, {Skipped} skipped, {Errors} errors, {Samples} samples added, took {Duration}",
            snapshot.Attempts,
            snapshot.Ok,
            snapshot.Skipped,
            snapshot.Errors,
            snapshot.SamplesAdded,
            snapshot.Duration.ToString(@"hh\:mm\:ss"));
    }

    private async Task UpdateSingleStationAsync(int stationId, CancellationToken cancellationToken)
    {
        _progressListener.OnIterationStarted(stationId);
        var outcome = IterationOutcome.Skipped;
        var samplesAdded = 0;

        try
        {
            var station = await _dataService.GetStationAsync(stationId, cancellationToken);
            if (station is null) return;

            _logger.LogDebug("Updating station {StationId} ({Spot})", stationId, station.Spot);

            var downloader = _downloaderFactory.GetDownloader(station.DownLoadType);
            if (downloader is null)
            {
                _logger.LogWarning("Station {StationId} has unknown DownLoadType {Type}", stationId, station.DownLoadType);
                outcome = IterationOutcome.Error;
                return;
            }

            if (string.IsNullOrEmpty(station.Link))
            {
                _logger.LogWarning("Station {StationId} has empty Link", stationId);
                outcome = IterationOutcome.Error;
                return;
            }

            var samples = await downloader.GetRecordsAsync(station.Link, cancellationToken);

            foreach (var sample in samples)
            {
                samplesAdded += await _dataService.AddSampleAsync(
                    station.Id, sample.Level, sample.Flow, sample.Temperature, sample.TimeStamp, cancellationToken);
            }

            _logger.LogDebug("Station {StationId} ok, {Count} samples added", stationId, samplesAdded);
            _errorTracker.RecordSuccess(stationId);
            outcome = IterationOutcome.Ok;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            outcome = IterationOutcome.Error;
            var withStack = _errorTracker.RecordError(stationId, ex);
            if (withStack)
            {
                _logger.LogWarning(ex,
                    "Station {StationId} failed: {ExceptionType} (next stack throttled for 1h)",
                    stationId, ex.GetType().Name);
            }
            else
            {
                _logger.LogWarning(
                    "Station {StationId} failed: {ExceptionType}: {Message}",
                    stationId, ex.GetType().Name, ex.Message);
            }
        }
        finally
        {
            _cycleStats.RecordIteration(stationId, outcome, samplesAdded);
            _progressListener.OnIterationCompleted(stationId);
        }
    }
}
