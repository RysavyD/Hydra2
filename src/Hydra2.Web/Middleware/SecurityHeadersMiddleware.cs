namespace Hydra2.Web.Middleware;

/// <summary>
/// Sets defense-in-depth security headers on every response.
/// After Phase 9, all client-side JS lives in compiled bundles
/// (/js/site.bundle.js, /js/graf.min.js) plus amCharts CDN — so script-src
/// no longer needs 'unsafe-inline'. style-src still does because Bootstrap 3
/// and Razor views use inline style="" attributes in many places.
/// </summary>
public class SecurityHeadersMiddleware
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' https://cdn.amcharts.com; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            if (!headers.ContainsKey("X-Content-Type-Options")) headers["X-Content-Type-Options"] = "nosniff";
            if (!headers.ContainsKey("X-Frame-Options")) headers["X-Frame-Options"] = "SAMEORIGIN";
            if (!headers.ContainsKey("Referrer-Policy")) headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            if (!headers.ContainsKey("Permissions-Policy")) headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
            if (!headers.ContainsKey("Content-Security-Policy")) headers["Content-Security-Policy"] = ContentSecurityPolicy;
            return Task.CompletedTask;
        });

        return _next(context);
    }
}
