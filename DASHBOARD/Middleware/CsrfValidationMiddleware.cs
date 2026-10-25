using DASHBOARD.Core.Constants;

namespace DASHBOARD.Middleware;

/// <summary>
/// Enforces the double-submit CSRF defence for cookie-authenticated requests.
/// <para>
/// When the browser sends the HttpOnly auth cookie automatically, a cross-site page could trigger
/// a state-changing request on the user's behalf. We block that by requiring the caller to also
/// echo the readable <c>XSRF-TOKEN</c> cookie back in the <c>X-XSRF-TOKEN</c> header: a cross-site
/// attacker can make the browser send the cookie but — same-origin policy — cannot read it to set
/// the header, so the two cannot match.
/// </para>
/// <para>
/// Only unsafe methods (POST/PUT/PATCH/DELETE) that authenticate <em>via the access cookie</em> are
/// checked. Requests bearing an <c>Authorization: Bearer</c> header (HUB SSO, service calls) or the
/// internal token are not cookie-driven and are exempt, as are safe (read) methods.
/// </para>
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
public sealed class CsrfValidationMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    /// <summary>Validates the double-submit token before forwarding the request.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;

        var isSafe = SafeMethods.Contains(request.Method);
        var usesAuthCookie = request.Cookies.ContainsKey(AppConstants.AccessTokenCookie);
        // A bearer token or internal token means the request is not relying on the ambient cookie,
        // so CSRF does not apply — those callers cannot be driven by a victim's browser.
        var usesBearer = request.Headers.ContainsKey("Authorization")
                         || request.Headers.ContainsKey("X-Internal-Token");

        if (!isSafe && usesAuthCookie && !usesBearer)
        {
            var cookieToken = request.Cookies[AppConstants.CsrfCookie];
            var headerToken = request.Headers[AppConstants.CsrfHeader].FirstOrDefault();

            if (string.IsNullOrEmpty(cookieToken) ||
                !string.Equals(cookieToken, headerToken, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("CSRF token missing or invalid.");
                return;
            }
        }

        await next(context);
    }
}
