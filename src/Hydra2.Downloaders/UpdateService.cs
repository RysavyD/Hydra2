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
    private readonly ILogger<UpdateService> _logger;

    public UpdateService(
        IDataService dataService,
        IConfigService configService,
        IDownloaderFactory downloaderFactory,
        IUpdateProgressListener progressListener,
        ILogger<UpdateService> logger)
    {
        _dataService = dataService;
        _configService = configService;
        _downloaderFactory = downloaderFactory;
        _progressListener = progressListener;
        _logger = logger;
    }

    public async Task UpdateSpotsAsync(int startIndex, int stopIndex, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Update Spots {Start}-{Stop}", startIndex, stopIndex);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");

        for (var i = startIndex; i <= stopIndex; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateSingleStationAsync(i, cancellationToken);
        }
    }

    public async Task LastSpotsLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start update loop.");
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
                _logger.LogError(ex, "Error in update loop iteration");
            }
        }
    }

    public async Task UpdateNextSpotAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configService.GetFirstConfigAsync(cancellationToken);
        _logger.LogInformation("Načten config: {Value}", config.Value);

        var nextValue = config.Value + 1;
        if (nextValue > MaxStationId) nextValue = 0;
        await _configService.UpdateConfigAsync(config.Id, nextValue, cancellationToken);

        await UpdateSingleStationAsync(nextValue, cancellationToken);
    }

    private async Task UpdateSingleStationAsync(int stationId, CancellationToken cancellationToken)
    {
        _progressListener.OnIterationStarted(stationId);
        try
        {
            var station = await _dataService.GetStationAsync(stationId, cancellationToken);
            if (station is null) return;

            _logger.LogInformation("Stanice: {Spot}", station.Spot);

            var downloader = _downloaderFactory.GetDownloader(station.DownLoadType);
            if (downloader is null)
            {
                _logger.LogWarning("Neznámý DownLoadType {Type} pro stanici {StationId}", station.DownLoadType, stationId);
                return;
            }

            _logger.LogDebug("DownloaderType: {Type}", downloader.GetType().Name);

            if (string.IsNullOrEmpty(station.Link)) return;

            var samples = await downloader.GetRecordsAsync(station.Link, cancellationToken);

            var inserted = 0;
            foreach (var sample in samples)
            {
                inserted += await _dataService.AddSampleAsync(
                    station.Id, sample.Level, sample.Flow, sample.Temperature, sample.TimeStamp, cancellationToken);
            }

            _logger.LogDebug("Vzorků: {Count}, ukládám.", inserted);
            _logger.LogInformation("Uloženo");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chyba při aktualizaci stanice {StationId}", stationId);
        }
        finally
        {
            _progressListener.OnIterationCompleted(stationId);
        }
    }
}
