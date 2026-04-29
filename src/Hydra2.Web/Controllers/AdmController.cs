using System.Globalization;
using System.Reflection;
using Hydra2.Downloaders;
using Hydra2.Service;
using Hydra2.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Hydra2.Web.Controllers;

public class AdmController : Controller
{
    private readonly IAdminService _adminService;
    private readonly IDataService _dataService;
    private readonly IUpdateService _updateService;
    private readonly IDownloaderFactory _downloaderFactory;
    private readonly ILogger<AdmController> _logger;

    public AdmController(
        IAdminService adminService,
        IDataService dataService,
        IUpdateService updateService,
        IDownloaderFactory downloaderFactory,
        ILogger<AdmController> logger)
    {
        _adminService = adminService;
        _dataService = dataService;
        _updateService = updateService;
        _downloaderFactory = downloaderFactory;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adm.Index");

        ViewBag.samplesCount = await _adminService.GetSamplesCountAsync(cancellationToken);

        var ci = CultureInfo.CurrentCulture;
        ViewBag.culture = ci.CompareInfo.Name + " - " + ci.DisplayName;
        ViewBag.date = System.IO.File.GetCreationTime(Assembly.GetExecutingAssembly().Location);

        return View();
    }

    public async Task<IActionResult> SpotOverView(bool samples = false, CancellationToken cancellationToken = default)
    {
        SpotOverviewViewModel[] model;
        if (samples)
        {
            model = (await _adminService.GetSpotOverviewWithSamplesAsync(cancellationToken))
                .Select(s => new SpotOverviewViewModel
                {
                    Id = s.Id,
                    RiverName = s.Name,
                    SpotName = s.Spot,
                    SpotType = s.Type,
                    Url = s.Link,
                    LastSample = s.LastSample,
                })
                .OrderBy(s => s.LastSample)
                .ToArray();
        }
        else
        {
            model = (await _adminService.GetSpotOverviewAsync(cancellationToken))
                .Select(s => new SpotOverviewViewModel
                {
                    Id = s.Id,
                    RiverName = s.Name,
                    SpotName = s.Spot,
                    SpotType = s.Type,
                    Url = s.Link,
                })
                .OrderBy(s => s.RiverName)
                .ThenBy(s => s.SpotName)
                .ToArray();
        }

        return View(model);
    }

    public async Task<IActionResult> GetStationOverView(int id, CancellationToken cancellationToken)
    {
        try
        {
            var station = await _dataService.GetStationAsync(id, cancellationToken);
            if (station is null)
            {
                ViewBag.Note = $"Stanice s id={id} nenalezena";
                return View();
            }

            _logger.LogInformation("Stanice: {Spot}", station.Spot);

            var downloader = _downloaderFactory.GetDownloader(station.DownLoadType);
            if (downloader is null || string.IsNullOrEmpty(station.Link))
            {
                ViewBag.Note = "Neznámý DownLoadType nebo chybějící Link";
                return View();
            }

            _logger.LogDebug("DownloaderType: {Type}", downloader.GetType().Name);
            var downloadSamples = await downloader.GetRecordsAsync(station.Link, cancellationToken);

            var sb = new System.Text.StringBuilder();
            foreach (var sample in downloadSamples)
            {
                sb.AppendLine($"{sample.TimeStamp} - h:{sample.Level}, q:{sample.Flow}, t:{sample.Temperature}");
                sb.AppendLine("<br />");
            }
            ViewBag.Note = sb.ToString();

            _logger.LogInformation("Uloženo");
        }
        catch (Exception ex)
        {
            ViewBag.Note = $"Výjimka: {ex.Message}";
            _logger.LogError(ex, "Chyba v GetStationOverView");
        }

        return View();
    }

    public IActionResult HandUpdate() => View();

    [HttpPost, ValidateAntiForgeryToken, ActionName("HandUpdate")]
    public async Task<IActionResult> HandUpdatePost(int stationId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ručně aktualizuji záznam s Id: {StationId}", stationId);
        await _updateService.UpdateSpotsAsync(stationId, stationId, cancellationToken);
        return View("HandUpdate");
    }
}
