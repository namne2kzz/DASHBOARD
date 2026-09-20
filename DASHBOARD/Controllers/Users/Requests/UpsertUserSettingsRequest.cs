namespace DASHBOARD.Controllers.Users.Requests;

/// <summary>
/// Payload for <c>PUT /api/users/me/settings</c>.
/// Each entry either inserts a new key or replaces an existing one.
/// A null value clears the stored value while keeping the row.
/// </summary>
/// <param name="Settings">Key-value pairs to persist. Must contain at least one entry; keys must be non-empty.</param>
public sealed record UpsertUserSettingsRequest(Dictionary<string, string?> Settings);
