using Hydra2.Downloaders;
using Hydra2.Service;
using Hydra2.Service.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hydra2.Web.Controllers;

public class ApiController : Controller
{
    private readonly IUpdateService _updateService;
    private readonly IDataService _dataService;
    private readonly IConfigService _configService;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        IUpdateService updateService,
        IDataService dataService,
        IConfigService configService,
        IOptions<AuthOptions> authOptions,
        ILogger<ApiController> logger)
    {
        _updateService = updateService;
        _dataService = dataService;
        _configService = configService;
        _authOptions = authOptions.Value;
        _logger = logger;
    }

    public async Task<IActionResult> HandUpdate(int id, string token, CancellationToken cancellationToken)
    {
        if (!IsAuthorized(token)) return Json("Bad token");

        await _updateService.UpdateSpotsAsync(id, id, cancellationToken);
        return Json($"Ok {id}");
    }

    [HttpPost]
    public async Task<IActionResult> ManualData(int stationId, string token, [FromBody] SpotRecord[] records, CancellationToken cancellationToken)
    {
        if (!IsAuthorized(token)) return Json("Bad token");

        try
        {
            var count = 0;
            foreach (var sample in records)
            {
                count += await _dataService.AddSampleAsync(
                    stationId, sample.Level, sample.Flow, sample.Temperature, sample.TimeStamp, cancellationToken);
            }
            return Json($"Ok {stationId}, {count}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ManualData failed for station {StationId}", stationId);
            return Json($"Error {ex.Message}");
        }
    }

    public async Task<IActionResult> UpdateNext(string token, CancellationToken cancellationToken)
    {
        if (!IsAuthorized(token)) return Json("Bad token");

        try
        {
            await _updateService.UpdateNextSpotAsync(cancellationToken);
            return Json("OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateNext failed");
            return Json($"Error {ex.Message}");
        }
    }

    public async Task<IActionResult> GetLast(string token, CancellationToken cancellationToken)
    {
        if (!IsAuthorized(token)) return Json("Bad token");

        try
        {
            var config = await _configService.GetFirstConfigAsync(cancellationToken);
            return Json(config.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLast failed");
            return Json($"Error {ex.Message}");
        }
    }

    private bool IsAuthorized(string? token) =>
        !string.IsNullOrEmpty(_authOptions.SecretToken) &&
        token == _authOptions.SecretToken;
}
