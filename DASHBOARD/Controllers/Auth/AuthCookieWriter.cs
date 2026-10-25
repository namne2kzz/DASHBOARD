using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Core.Constants;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Controllers.Auth;

/// <summary>Strongly-typed auth-cookie options bound from the <c>AuthCookie</c> configuration section.</summary>
public sealed class AuthCookieOptions
{
    /// <summary>Whether cookies carry the <c>Secure</c> flag (HTTPS-only). True in production.</summary>
    public bool Secure { get; init; } = true;

    /// <summary>SameSite policy for the auth cookies (<c>Lax</c>, <c>Strict</c> or <c>None</c>).</summary>
    public string SameSite { get; init; } = "Lax";
}

/// <summary>
/// Writes and clears the HttpOnly auth cookies and the readable double-submit CSRF cookie.
/// Lives in the Web layer because it needs <see cref="HttpResponse"/> — never reference it
/// from Application/Domain.
/// </summary>
/// <param name="options">Cookie security options bound from configuration.</param>
public sealed class AuthCookieWriter(IOptions<AuthCookieOptions> options)
{
    private readonly AuthCookieOptions _opt = options.Value;

    private SameSiteMode SameSite => _opt.SameSite.ToLowerInvariant() switch
    {
        "strict" => SameSiteMode.Strict,
        "none"   => SameSiteMode.None,
        _        => SameSiteMode.Lax,
    };

    /// <summary>
    /// Writes the access, refresh and CSRF cookies onto the response for a freshly issued session.
    /// The access/refresh cookies are HttpOnly (invisible to JS); the CSRF cookie is deliberately
    /// readable so the SPA can echo it back in a header (double-submit pattern).
    /// </summary>
    /// <param name="response">The HTTP response to attach cookies to.</param>
    /// <param name="session">The issued session carrying tokens and their expiry timestamps.</param>
    public void WriteSession(HttpResponse response, LoginResult session)
    {
        // Cookie lifetime tracks token lifetime so a stale cookie never lingers past the token it holds.
        var accessExpiry  = session.AccessTokenExpiresAt;
        var refreshExpiry = session.RefreshTokenExpiresAt;

        response.Cookies.Append(AppConstants.AccessTokenCookie, session.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure   = _opt.Secure,
            SameSite = SameSite,
            Path     = "/",
            Expires  = accessExpiry,
        });

        response.Cookies.Append(AppConstants.RefreshTokenCookie, session.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure   = _opt.Secure,
            SameSite = SameSite,
            // Scope the refresh cookie to the refresh endpoint only, so it is not sent on every API
            // call — it is needed solely when exchanging it at /api/v{n}/auth/refresh.
            Path     = "/api",
            Expires  = refreshExpiry,
        });

        // A fresh CSRF secret per session. Readable by JS on purpose: Angular's HttpClient copies it
        // into the X-XSRF-TOKEN header, which the server compares to this cookie (double submit).
        response.Cookies.Append(AppConstants.CsrfCookie, Guid.NewGuid().ToString("N"), new CookieOptions
        {
            HttpOnly = false,
            Secure   = _opt.Secure,
            SameSite = SameSite,
            Path     = "/",
            Expires  = accessExpiry,
        });
    }

    /// <summary>Expires all three auth cookies — called on logout and on a failed refresh.</summary>
    /// <param name="response">The HTTP response to clear cookies on.</param>
    public void ClearSession(HttpResponse response)
    {
        // Delete options must match the Path/Secure/SameSite the cookie was written with, or the
        // browser keeps the original cookie.
        var common = new CookieOptions { Secure = _opt.Secure, SameSite = SameSite, Path = "/" };
        response.Cookies.Delete(AppConstants.AccessTokenCookie, common);
        response.Cookies.Delete(AppConstants.CsrfCookie, common);
        response.Cookies.Delete(AppConstants.RefreshTokenCookie,
            new CookieOptions { Secure = _opt.Secure, SameSite = SameSite, Path = "/api" });
    }
}
