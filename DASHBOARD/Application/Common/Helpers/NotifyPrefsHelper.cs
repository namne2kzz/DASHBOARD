using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Common.Helpers;

/// <summary>Reads per-user notification opt-in settings in a single query.</summary>
public static class NotifyPrefsHelper
{
    /// <summary>Maps a notification type to the category setting key that gates it; null for types with no category gate (e.g. mentions, handled separately).</summary>
    public static string? CategoryKey(NotificationType type) => type switch
    {
        NotificationType.WorkItemAssigned
            or NotificationType.WorkItemUnassigned
            or NotificationType.WorkItemStateChanged => UserSettingKeys.NotifyTypeWorkItems,
        NotificationType.SprintActivated
            or NotificationType.SprintClosed => UserSettingKeys.NotifyTypeSprints,
        NotificationType.AddedToRepository
            or NotificationType.RemovedFromRepository
            or NotificationType.AddedToSprint
            or NotificationType.RemovedFromSprint => UserSettingKeys.NotifyTypeMembership,
        _ => null,
    };

    /// <summary>
    /// Returns whether a push notification of the given type should reach the user:
    /// the global push master must be on AND the type's category must not be disabled.
    /// Category defaults to enabled when absent (opt-out).
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="userId">Target user ID.</param>
    /// <param name="type">The notification type being sent.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<bool> PushEnabledForTypeAsync(
        IApplicationDbContext db, Guid userId, NotificationType type, CancellationToken ct)
    {
        if (!await PushEnabledAsync(db, userId, ct))
            return false;

        var categoryKey = CategoryKey(type);
        if (categoryKey is null)
            return true;

        return await GetSettingBoolAsync(db, userId, categoryKey, defaultValue: true, ct);
    }

    /// <summary>
    /// Returns whether a user has opted into email notifications.
    /// Defaults to <c>true</c> when the setting is absent (opt-in by default).
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="userId">Target user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<bool> EmailEnabledAsync(
        IApplicationDbContext db, Guid userId, CancellationToken ct)
        => await GetSettingBoolAsync(db, userId, UserSettingKeys.NotifyEmail, defaultValue: true, ct);

    /// <summary>
    /// Returns whether a user has opted into in-app push notifications.
    /// Defaults to <c>false</c> when the setting is absent (off by default until user enables).
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="userId">Target user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<bool> PushEnabledAsync(
        IApplicationDbContext db, Guid userId, CancellationToken ct)
        => await GetSettingBoolAsync(db, userId, UserSettingKeys.NotifyPush, defaultValue: false, ct);

    /// <summary>
    /// Batch-reads the mention-notification opt-in for multiple users in one query.
    /// Independent of the push setting; defaults to <c>true</c> when absent (opt-out).
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="userIds">Users to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of userId → whether mention notifications are enabled.</returns>
    public static async Task<Dictionary<Guid, bool>> MentionBatchAsync(
        IApplicationDbContext db, IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();

        var rows = await db.Set<UserSetting>()
            .AsNoTracking()
            .Where(s => ids.Contains(s.UserId) && s.Key == UserSettingKeys.NotifyMentions)
            .Select(s => new { s.UserId, s.Value })
            .ToListAsync(ct);

        return ids.ToDictionary(id => id, id =>
        {
            var val = rows.FirstOrDefault(r => r.UserId == id)?.Value;
            return val is null ? true : val == "true";
        });
    }

    /// <summary>
    /// Batch-fetches both email and push prefs for multiple users in a single query.
    /// Returns a dictionary keyed by userId.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="userIds">Collection of user IDs to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<Dictionary<Guid, (bool Email, bool Push)>> GetBatchAsync(
        IApplicationDbContext db, IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids  = userIds.Distinct().ToList();
        var keys = new[] { UserSettingKeys.NotifyEmail, UserSettingKeys.NotifyPush };

        var rows = await db.Set<UserSetting>()
            .AsNoTracking()
            .Where(s => ids.Contains(s.UserId) && keys.Contains(s.Key))
            .Select(s => new { s.UserId, s.Key, s.Value })
            .ToListAsync(ct);

        return ids.ToDictionary(id => id, id =>
        {
            var userRows = rows.Where(r => r.UserId == id).ToList();
            var emailVal = userRows.FirstOrDefault(r => r.Key == UserSettingKeys.NotifyEmail)?.Value;
            var pushVal  = userRows.FirstOrDefault(r => r.Key == UserSettingKeys.NotifyPush)?.Value;

            // Email defaults true (opt-in by default); push defaults false (opt-in required).
            var email = emailVal is null ? true  : emailVal == "true";
            var push  = pushVal  is null ? false : pushVal  == "true";
            return (email, push);
        });
    }

    /// <summary>
    /// Batch-reads email opt-in and type-aware push opt-in for multiple users in a single query.
    /// Push is true only when the global push master is on AND the type's category is not disabled.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="userIds">Users to look up.</param>
    /// <param name="type">The notification type being sent (selects the category gate).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of userId → (Email enabled, Push enabled for this type).</returns>
    public static async Task<Dictionary<Guid, (bool Email, bool Push)>> GetBatchForTypeAsync(
        IApplicationDbContext db, IEnumerable<Guid> userIds, NotificationType type, CancellationToken ct)
    {
        var ids         = userIds.Distinct().ToList();
        var categoryKey = CategoryKey(type);
        var keys = categoryKey is null
            ? [UserSettingKeys.NotifyEmail, UserSettingKeys.NotifyPush]
            : new[] { UserSettingKeys.NotifyEmail, UserSettingKeys.NotifyPush, categoryKey };

        var rows = await db.Set<UserSetting>()
            .AsNoTracking()
            .Where(s => ids.Contains(s.UserId) && keys.Contains(s.Key))
            .Select(s => new { s.UserId, s.Key, s.Value })
            .ToListAsync(ct);

        return ids.ToDictionary(id => id, id =>
        {
            var userRows = rows.Where(r => r.UserId == id).ToList();
            var emailVal = userRows.FirstOrDefault(r => r.Key == UserSettingKeys.NotifyEmail)?.Value;
            var pushVal  = userRows.FirstOrDefault(r => r.Key == UserSettingKeys.NotifyPush)?.Value;

            var email = emailVal is null ? true  : emailVal == "true";
            var push  = pushVal  is null ? false : pushVal  == "true";

            // Apply the category gate (defaults to enabled when absent).
            if (push && categoryKey is not null)
            {
                var catVal = userRows.FirstOrDefault(r => r.Key == categoryKey)?.Value;
                push = catVal is null || catVal == "true";
            }

            return (email, push);
        });
    }

    private static async Task<bool> GetSettingBoolAsync(
        IApplicationDbContext db, Guid userId, string key, bool defaultValue, CancellationToken ct)
    {
        var value = await db.Set<UserSetting>()
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        return value is null ? defaultValue : value == "true";
    }
}
