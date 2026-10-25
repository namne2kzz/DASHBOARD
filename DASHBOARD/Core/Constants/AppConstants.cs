namespace DASHBOARD.Core.Constants;

/// <summary>Application-wide string constants shared across all layers.</summary>
public static class AppConstants
{
    /// <summary>The default SQL Server schema used for all tables.</summary>
    public const string DefaultSchema = "dbo";

    /// <summary>JWT claim type carrying the user's <see cref="Guid"/> identifier.</summary>
    public const string UserIdClaim = "uid";

    /// <summary>JWT claim type carrying the user's display name.</summary>
    public const string UserNameClaim = "name";

    /// <summary>JWT claim type carrying the user's organization (tenant) <see cref="Guid"/> identifier.</summary>
    public const string OrgIdClaim = "org";

    /// <summary>Named CORS policy allowing requests from the Angular dev server.</summary>
    public const string AngularCorsPolicy = "AllowAngular";

    // ── Auth cookies ───────────────────────────────────────────────────────────
    /// <summary>Name of the HttpOnly cookie carrying the JWT access token.</summary>
    public const string AccessTokenCookie = "nflow_at";

    /// <summary>Name of the HttpOnly cookie carrying the opaque refresh token.</summary>
    public const string RefreshTokenCookie = "nflow_rt";

    /// <summary>
    /// Name of the readable (non-HttpOnly) CSRF cookie. Must match the Angular
    /// <c>withXsrfConfiguration</c> cookieName so the SPA echoes it back in a header.
    /// </summary>
    public const string CsrfCookie = "XSRF-TOKEN";

    /// <summary>Request header the SPA uses to echo the CSRF cookie value (double-submit).</summary>
    public const string CsrfHeader = "X-XSRF-TOKEN";
}
