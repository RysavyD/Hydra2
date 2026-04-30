namespace Hydra2.Web.Middleware;

/// <summary>
/// Sets defense-in-depth security headers on every response.
/// CSP is intentionally permissive on inline scripts because Razor views
/// (Graf/Index.cshtml) still embed JS directly. After Phase 9 (TypeScript
/// extraction) this should tighten with nonces or hashes and drop 'unsafe-inline'.
/// </summary>
public class SecurityHeadersMiddleware
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.amcharts.com; " +
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
