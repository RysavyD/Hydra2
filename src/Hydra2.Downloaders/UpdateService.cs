using System.Globalization;
using Hydra2.Service;
using Hydra2.Service.Data;
using Microsoft.Extensions.Logging;

namespace Hydra2.Downloaders;

public class UpdateService : IUpdateService
{
    private readonly IDataService _dataService;
    private readonly IDownloaderFactory _downloaderFactory;
    private readonly IUpdateProgressListener _progressListener;
    private readonly IStationErrorTracker _errorTracker;
    private readonly ISourceStateTracker _sourceStateTracker;
    private readonly ILogger<UpdateService> _logger;

    public UpdateService(
        IDataService dataService,
        IDownloaderFactory downloaderFactory,
        IUpdateProgressListener progressListener,
        IStationErrorTracker errorTracker,
        ISourceStateTracker sourceStateTracker,
        ILogger<UpdateService> logger)
    {
        _dataService = dataService;
        _downloaderFactory = downloaderFactory;
        _progressListener = progressListener;
        _errorTracker = errorTracker;
        _sourceStateTracker = sourceStateTracker;
        _logger = logger;
    }

    public async Task<SourceRunOutcome> UpdateSourceAsync(int downLoadType, CancellationToken cancellationToken = default)
    {
        var sourceName = SourceCatalog.NameFor(downLoadType);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");

        var stations = await _dataService.GetStationsByDownLoadTypeAsync(downLoadType, cancellationToken);

        _sourceStateTracker.RecordRunStarted(downLoadType, stations.Count);
        _logger.LogInformation(
            "Source {Source} run started ({StationCount} stations)",
            sourceName, stations.Count);

        var ok = 0;
        var errors = 0;
        var samplesAdded = 0;
        string? lastErrorMessage = null;

        foreach (var station in stations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (stationOk, stationSamples, stationError) = await UpdateStationCoreAsync(station, cancellationToken);
            if (stationOk) { ok++; samplesAdded += stationSamples; }
            else { errors++; lastErrorMessage = stationError ?? lastErrorMessage; }
        }

        var outcome = errors switch
        {
            0 => SourceRunOutcome.Success,
            _ when ok == 0 => SourceRunOutcome.Failure,
            _ => SourceRunOutcome.PartialFailure,
        };

        _sourceStateTracker.RecordRunCompleted(downLoadType, outcome, ok, errors, samplesAdded, lastErrorMessage);

        _logger.LogInformation(
            "Source {Source} run complete: outcome={Outcome}, {Ok} ok, {Errors} errors, {Samples} samples added",
            sourceName, outcome, ok, errors, samplesAdded);

        return outcome;
    }

    public async Task UpdateStationAsync(int stationId, CancellationToken cancellationToken = default)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");
        var station = await _dataService.GetStationAsync(stationId, cancellationToken);
        if (station is null)
        {
            _logger.LogWarning("Station {StationId} not found", stationId);
            return;
        }
        await UpdateStationCoreAsync(station, cancellationToken);
    }

    public async Task UpdateSpotsAsync(int startIndex, int stopIndex, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual update {Start}-{Stop} started", startIndex, stopIndex);
        for (var i = startIndex; i <= stopIndex; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateStationAsync(i, cancellationToken);
        }
        _logger.LogInformation("Manual update {Start}-{Stop} finished", startIndex, stopIndex);
    }

    private async Task<(bool Ok, int SamplesAdded, string? ErrorMessage)> UpdateStationCoreAsync(Station station, CancellationToken cancellationToken)
    {
        var stationId = station.Id;
        _progressListener.OnIterationStarted(stationId);

        try
        {
            _logger.LogDebug("Updating station {StationId} ({Spot})", stationId, station.Spot);

            var downloader = _downloaderFactory.GetDownloader(station.DownLoadType);
            if (downloader is null)
            {
                _logger.LogWarning("Station {StationId} has unknown DownLoadType {Type}", stationId, station.DownLoadType);
                return (false, 0, $"Unknown DownLoadType {station.DownLoadType}");
            }

            if (string.IsNullOrEmpty(station.Link))
            {
                _logger.LogWarning("Station {StationId} has empty Link", stationId);
                return (false, 0, "Empty Link");
            }

            var samples = await downloader.GetRecordsAsync(station.Link, cancellationToken);

            var samplesAdded = 0;
            foreach (var sample in samples)
            {
                samplesAdded += await _dataService.AddSampleAsync(
                    station.Id, sample.Level, sample.Flow, sample.Temperature, sample.TimeStamp, cancellationToken);
            }

            _logger.LogWarning("Station {StationId} ok, {Count} samples added", stationId, samplesAdded);
            _errorTracker.RecordSuccess(stationId);
            return (true, samplesAdded, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
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
            return (false, 0, $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _progressListener.OnIterationCompleted(stationId);
        }
    }
}
