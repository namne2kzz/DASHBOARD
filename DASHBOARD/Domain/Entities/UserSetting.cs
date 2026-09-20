namespace DASHBOARD.Domain.Entities;

/// <summary>
/// A single key-value display/preference setting for a user.
/// The table is intentionally schema-free (key-value) so new settings can be added
/// without database migrations — only a new string key is needed.
/// </summary>
public sealed class UserSetting : Common.BaseEntity
{
    /// <summary>Gets the owner's user ID.</summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the setting key — a stable dot-separated token, e.g. <c>"ui.date-format"</c>.
    /// Use <see cref="UserSettingKeys"/> constants for all read/write operations.
    /// </summary>
    public string Key { get; private set; } = default!;

    /// <summary>
    /// Gets or sets the serialised string value.
    /// Null means the setting is unset and the application should use its default.
    /// </summary>
    public string? Value { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    /// <summary>Setting owner.</summary>
    public User User { get; private set; } = default!;

    // ── Factory / mutation ───────────────────────────────────────────────────

    /// <summary>Creates a new user setting row.</summary>
    /// <param name="userId">Owner.</param>
    /// <param name="key">Stable setting key (use <see cref="UserSettingKeys"/>).</param>
    /// <param name="value">Serialised string value, or null to clear.</param>
    /// <returns>A new <see cref="UserSetting"/> instance.</returns>
    public static UserSetting Create(Guid userId, string key, string? value) => new()
    {
        UserId = userId,
        Key    = key,
        Value  = value,
    };

    /// <summary>Updates the value in-place and stamps <see cref="Common.BaseEntity.UpdatedAt"/>.</summary>
    /// <param name="value">New serialised string value, or null to clear.</param>
    public void SetValue(string? value)
    {
        Value = value;
        Touch();
    }
}
