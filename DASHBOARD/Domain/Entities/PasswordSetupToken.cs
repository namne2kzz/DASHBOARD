namespace DASHBOARD.Domain.Entities;

/// <summary>
/// One-time token letting a newly provisioned user choose their own password.
/// </summary>
/// <remarks>
/// Issued when NKey provisions an organisation: the first admin account is created with an empty
/// <see cref="User.PasswordHash"/> and a link carrying the raw token is emailed instead. The password
/// therefore never travels by email, and the account cannot be signed into until it is set.
/// <para>Distinct from <see cref="Invitation"/>, which always targets a repository — an organisation
/// admin has no repository yet at provisioning time.</para>
/// </remarks>
public sealed class PasswordSetupToken : Common.BaseEntity
{
    /// <summary>Gets or sets the user who will set their password with this token.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the raw token embedded in the setup link.
    /// The raw token is never persisted — only this hash is stored and used for lookup.
    /// </summary>
    public string TokenHash { get; set; } = default!;

    /// <summary>Gets or sets the UTC timestamp after which this token can no longer be used.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the password was set. Null until used.</summary>
    public DateTime? UsedAt { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────

    /// <summary>The user this token belongs to.</summary>
    public User? User { get; set; }

    /// <summary>Gets a value indicating whether this token can still be redeemed.</summary>
    /// <param name="now">Current UTC time, injected so the check stays testable.</param>
    /// <returns><c>true</c> when the token is unused and not past <see cref="ExpiresAt"/>.</returns>
    public bool IsRedeemable(DateTime now) => UsedAt is null && now <= ExpiresAt;
}
