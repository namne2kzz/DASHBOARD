namespace DASHBOARD.Infrastructure.Identity;

/// <summary>Strongly-typed configuration bound from the <c>JwtSettings</c> section in appsettings.json.</summary>
public sealed class JwtSettings
{
    /// <summary>Gets or sets the HMAC-SHA256 signing secret (minimum 32 characters).</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Gets or sets the token issuer (must match <c>ValidIssuer</c> on validation).</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Gets or sets the token audience (must match <c>ValidAudience</c> on validation).</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Gets or sets how many hours the access token remains valid after issuance. Defaults to 8.</summary>
    public int ExpiresInHours { get; set; } = 8;

    /// <summary>Gets or sets how many days the refresh token remains valid after issuance. Defaults to 7.</summary>
    public int RefreshTokenExpiresInDays { get; set; } = 7;
}
