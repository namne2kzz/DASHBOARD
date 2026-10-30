using System.Security.Cryptography;
using System.Text;
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
/// <para>
/// A small set of anonymous entry points is also exempt: they carry their own credential in the body
/// and ignore the cookie entirely, so an existing session neither helps nor hinders them. Without
/// this, somebody already signed in could not open a password-setup link from their inbox.
/// </para>
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
public sealed class CsrfValidationMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    /// <summary>
    /// Anonymous endpoints that authenticate from the request body, not the ambient cookie.
    /// They establish a session rather than consuming one, so CSRF has nothing to protect.
    /// </summary>
    private static readonly string[] CredentialInBodyPaths =
    [
        "/auth/login",
        "/auth/google-login",
        "/auth/set-password",
        "/auth/refresh",
    ];

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

        // Matched on a suffix so the api/v{version} prefix does not have to be hard-coded here.
        var path = request.Path.Value ?? string.Empty;
        var carriesOwnCredential = CredentialInBodyPaths.Any(p =>
            path.EndsWith(p, StringComparison.OrdinalIgnoreCase));

        if (!isSafe && usesAuthCookie && !usesBearer && !carriesOwnCredential)
        {
            var cookieToken = request.Cookies[AppConstants.CsrfCookie];
            var headerToken = request.Headers[AppConstants.CsrfHeader].FirstOrDefault();

            if (!TokensMatch(cookieToken, headerToken))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("CSRF token missing or invalid.");
                return;
            }
        }

        await next(context);
    }

    /// <summary>
    /// Compares the double-submit pair without revealing how much of it matched.
    /// </summary>
    /// <remarks>
    /// <see cref="string.Equals(string?, string?, StringComparison)"/> stops at the first byte that
    /// differs, so the time taken to reject a guess grows with the length of its matching prefix.
    /// That turns the header into an oracle: a caller who can already make the browser send the
    /// cookie — the situation this guard exists for — could read the secret back a character at a
    /// time and then forge the header it was missing.
    /// <para>
    /// Comparing lengths first is not a leak. The CSRF secret is a fixed-length GUID written by
    /// this service, so its length is already public; only the value is secret.
    /// </para>
    /// </remarks>
    /// <param name="cookieToken">Token from the readable CSRF cookie.</param>
    /// <param name="headerToken">Token echoed back in the request header.</param>
    /// <returns>True when both are present and identical.</returns>
    private static bool TokensMatch(string? cookieToken, string? headerToken)
    {
        if (string.IsNullOrEmpty(cookieToken) || string.IsNullOrEmpty(headerToken)) return false;

        var cookieBytes = Encoding.UTF8.GetBytes(cookieToken);
        var headerBytes = Encoding.UTF8.GetBytes(headerToken);

        return CryptographicOperations.FixedTimeEquals(cookieBytes, headerBytes);
    }
}
