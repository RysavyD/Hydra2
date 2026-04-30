using Hydra2.Downloaders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serilog.Core;
using Serilog.Events;

namespace Hydra2.Web.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminApiController : ControllerBase
{
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly IStationErrorTracker _errorTracker;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<AdminApiController> _logger;

    public AdminApiController(
        LoggingLevelSwitch levelSwitch,
        IStationErrorTracker errorTracker,
        IOptions<AuthOptions> authOptions,
        ILogger<AdminApiController> logger)
    {
        _levelSwitch = levelSwitch;
        _errorTracker = errorTracker;
        _authOptions = authOptions.Value;
        _logger = logger;
    }

    [HttpGet("logs/level")]
    public IActionResult GetLogLevel(string? token)
    {
        if (!IsAuthorized(token)) return Unauthorized();
        return Ok(new { current = _levelSwitch.MinimumLevel.ToString() });
    }

    [HttpPost("logs/level")]
    public IActionResult SetLogLevel(string? token, string? level)
    {
        if (!IsAuthorized(token)) return Unauthorized();

        if (string.IsNullOrEmpty(level) || !Enum.TryParse<LogEventLevel>(level, ignoreCase: true, out var parsed))
        {
            return BadRequest(new
            {
                error = $"Unknown level '{level}'.",
                allowed = Enum.GetNames<LogEventLevel>(),
            });
        }

        var previous = _levelSwitch.MinimumLevel;
        _levelSwitch.MinimumLevel = parsed;
        _logger.LogWarning("Log level changed from {Previous} to {New} via admin API", previous, parsed);

        return Ok(new { previous = previous.ToString(), current = parsed.ToString() });
    }

    [HttpGet("failing-stations")]
    public IActionResult GetFailingStations(string? token, int? topN = null, double? minErrorRate = null)
    {
        if (!IsAuthorized(token)) return Unauthorized();

        var report = _errorTracker.GetReport(topN);
        if (minErrorRate.HasValue)
        {
            report = report.Where(r => r.ErrorRate >= minErrorRate.Value).ToArray();
        }

        return Ok(new
        {
            count = report.Count,
            generatedAt = DateTime.UtcNow,
            stations = report.Select(r => new
            {
                stationId = r.StationId,
                errors = r.ErrorCount,
                successes = r.SuccessCount,
                errorRate = Math.Round(r.ErrorRate, 3),
                firstError = r.FirstError,
                lastError = r.LastError,
                errorsByType = r.ErrorsByType,
            }),
        });
    }

    private bool IsAuthorized(string? token) =>
        !string.IsNullOrEmpty(_authOptions.SecretToken) &&
        token == _authOptions.SecretToken;
}
