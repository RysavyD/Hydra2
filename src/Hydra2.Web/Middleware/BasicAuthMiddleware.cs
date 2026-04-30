using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hydra2.Web.Middleware;

/// <summary>
/// Protects configured path prefixes (default: /Adm) with HTTP Basic authentication.
/// Skipped entirely in Development. Returns 503 if AdminAuth credentials are not configured
/// in non-Development environments — forces operators to set credentials before deploying.
/// </summary>
public class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<AdminAuthOptions> _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<BasicAuthMiddleware> _logger;

    public BasicAuthMiddleware(
        RequestDelegate next,
        IOptionsMonitor<AdminAuthOptions> options,
        IHostEnvironment env,
        ILogger<BasicAuthMiddleware> logger)
    {
        _next = next;
        _options = options;
        _env = env;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var opts = _options.CurrentValue;

        if (!IsProtectedPath(path, opts) || _env.IsDevelopment())
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrEmpty(opts.Username) || string.IsNullOrEmpty(opts.Password))
        {
            _logger.LogError("AdminAuth not configured in {Env} - blocking {Path}", _env.EnvironmentName, path);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Admin section is not configured. Set AdminAuth:Username and AdminAuth:Password.");
            return;
        }

        if (!TryValidate(context.Request.Headers.Authorization, opts))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = $"Basic realm=\"{opts.Realm}\", charset=\"UTF-8\"";
            return;
        }

        await _next(context);
    }

    private static bool IsProtectedPath(string path, AdminAuthOptions opts) =>
        opts.ProtectedPaths.Any(prefix =>
            path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            (path.Length == prefix.Length || path[prefix.Length] == '/'));

    private static bool TryValidate(string? authorization, AdminAuthOptions opts)
    {
        if (string.IsNullOrEmpty(authorization)) return false;
        if (!AuthenticationHeaderValue.TryParse(authorization, out var header)) return false;
        if (!string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrEmpty(header.Parameter)) return false;

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
        }
        catch (FormatException)
        {
            return false;
        }

        var separator = decoded.IndexOf(':');
        if (separator < 0) return false;

        var user = decoded[..separator];
        var pass = decoded[(separator + 1)..];

        return ConstantTimeEquals(user, opts.Username) && ConstantTimeEquals(pass, opts.Password);
    }

    private static bool ConstantTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));
}
