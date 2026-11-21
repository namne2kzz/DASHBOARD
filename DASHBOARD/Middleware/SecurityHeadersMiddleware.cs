namespace DASHBOARD.Middleware;

/// <summary>
/// Adds HTTP security response headers (CSP, X-Frame-Options, X-Content-Type-Options,
/// Referrer-Policy, Permissions-Policy) to every response so the browser enforces an extra
/// layer of defence independent of the application code.
/// </summary>
/// <remarks>
/// These are defence-in-depth headers, not a replacement for input handling: the Content-Security-Policy
/// limits what the browser will execute or load even if an XSS payload slips past Angular's sanitiser,
/// and X-Frame-Options / frame-ancestors block clickjacking. The middleware runs early in the pipeline
/// so the headers are present on error responses too; headers are written via <see cref="HttpResponse.OnStarting"/>
/// because they cannot be set once the response body has begun.
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Content-Security-Policy. Scripts and styles are restricted to the app's own origin; framing is
    /// denied; base-uri and form-action are pinned to 'self'. 'unsafe-inline' is kept for style-src only
    /// because Angular injects inline component styles at runtime — script-src stays strict.
    /// </summary>
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: blob:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    /// <summary>Writes the security headers for the current response, then invokes the rest of the pipeline.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Only set when absent so a specific endpoint can override (e.g. a page that must be framed).
            headers.TryAdd("Content-Security-Policy", ContentSecurityPolicy);
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

            return Task.CompletedTask;
        });

        await next(context);
    }
}
