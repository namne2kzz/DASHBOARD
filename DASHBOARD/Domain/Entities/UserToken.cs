namespace DASHBOARD.Domain.Entities;

/// <summary>
/// Tracks issued JWT + refresh token pairs per user.
/// Access token revocation uses <see cref="IsRevoked"/> / <see cref="JwtId"/>.
/// Refresh token is stored as a SHA-256 hash (<see cref="RefreshTokenHash"/>) — never plaintext.
/// Token rotation: each successful refresh revokes this record and creates a new one.
/// </summary>
public sealed class UserToken : Common.BaseEntity
{
    /// <summary>Gets or sets the user who owns this token pair.</summary>
    public Guid UserId { get; set; }

    // ── Access token ─────────────────────────────────────────────────────────

    /// <summary>Gets or sets the JWT ID (<c>jti</c> claim) — unique identifier for this specific access token.</summary>
    public string JwtId { get; set; } = default!;

    /// <summary>Gets or sets the UTC expiry time of the access token (mirrors <c>exp</c> claim).</summary>
    public DateTime AccessTokenExpiresAt { get; set; }

    /// <summary>Gets or sets whether the access token has been explicitly revoked (e.g. via logout).</summary>
    public bool IsRevoked { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the access token was revoked. Null if still valid.</summary>
    public DateTime? RevokedAt { get; set; }

    // ── Refresh token ─────────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the SHA-256 hash of the opaque refresh token.
    /// The plaintext token is returned to the client on login/refresh and never stored here.
    /// </summary>
    public string RefreshTokenHash { get; set; } = default!;

    /// <summary>Gets or sets the UTC expiry time of the refresh token.</summary>
    public DateTime RefreshTokenExpiresAt { get; set; }

    /// <summary>Gets or sets whether the refresh token has been revoked (used, logged out, or rotated).</summary>
    public bool RefreshTokenIsRevoked { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the refresh token was revoked. Null if still valid.</summary>
    public DateTime? RefreshTokenRevokedAt { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Token owner.</summary>
    public User? User { get; set; }
}
