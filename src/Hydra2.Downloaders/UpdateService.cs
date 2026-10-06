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
            "Zdroj {Source}: beh zahajen ({StationCount} stanic)",
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
            "Zdroj {Source}: beh dokoncen, vysledek={Outcome}, {Ok} v poradku, {Errors} chyb, pridano {Samples} vzorku",
            sourceName, outcome, ok, errors, samplesAdded);

        return outcome;
    }

    public async Task UpdateStationAsync(int stationId, CancellationToken cancellationToken = default)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");
        var station = await _dataService.GetStationAsync(stationId, cancellationToken);
        if (station is null)
        {
            _logger.LogWarning("Stanice ID {StationId} nenalezena", stationId);
            return;
        }
        await UpdateStationCoreAsync(station, cancellationToken);
    }

    public async Task UpdateSpotsAsync(int startIndex, int stopIndex, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rucni aktualizace stanic {Start}-{Stop} zahajena", startIndex, stopIndex);
        for (var i = startIndex; i <= stopIndex; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateStationAsync(i, cancellationToken);
        }
        _logger.LogInformation("Rucni aktualizace stanic {Start}-{Stop} dokoncena", startIndex, stopIndex);
    }

    private async Task<(bool Ok, int SamplesAdded, string? ErrorMessage)> UpdateStationCoreAsync(Station station, CancellationToken cancellationToken)
    {
        var stationId = station.Id;
        _progressListener.OnIterationStarted(stationId);

        var phase = "priprava";

        try
        {
            var downloader = _downloaderFactory.GetDownloader(station.DownLoadType);
            if (downloader is null)
            {
                _logger.LogWarning("Stanice ID {StationId}: neznamy DownLoadType {Type}", stationId, station.DownLoadType);
                return (false, 0, $"Neznamy DownLoadType {station.DownLoadType}");
            }

            if (string.IsNullOrEmpty(station.Link))
            {
                _logger.LogWarning("Stanice ID {StationId}: prazdny Link", stationId);
                return (false, 0, "Prazdny Link");
            }

            phase = "stahovani";
            _logger.LogInformation("Stahuji stanici ID {StationId} z URL {Link}", stationId, station.Link);
            var samples = await downloader.GetRecordsAsync(station.Link, cancellationToken);
            _logger.LogInformation("Stanice ID {StationId}: stahovani uspesne skonceno", stationId);

            phase = "ukladani";
            _logger.LogInformation("Stanice ID {StationId}: nalezeno {Found} vzorku, ukladam", stationId, samples.Count);
            var samplesAdded = 0;
            foreach (var sample in samples)
            {
                samplesAdded += await _dataService.AddSampleAsync(
                    station.Id, sample.Level, sample.Flow, sample.Temperature, sample.TimeStamp, cancellationToken);
            }
            _logger.LogInformation("Stanice ID {StationId}: ulozeno {Saved} vzorku", stationId, samplesAdded);

            _errorTracker.RecordSuccess(stationId);
            _logger.LogInformation("Stanice ID {StationId} hotova", stationId);
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
                    "Stanice ID {StationId}: chyba ve fazi {Phase}: {ExceptionType}: {Message} (dalsi stack trace potlacen na 1 h)",
                    stationId, phase, ex.GetType().Name, ex.Message);
            }
            else
            {
                _logger.LogWarning(
                    "Stanice ID {StationId}: chyba ve fazi {Phase}: {ExceptionType}: {Message}",
                    stationId, phase, ex.GetType().Name, ex.Message);
            }
            return (false, 0, $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _progressListener.OnIterationCompleted(stationId);
        }
    }
}
