namespace DASHBOARD.Domain.Entities;

/// <summary>
/// Canonical setting keys stored in <see cref="UserSetting"/>.
/// Add new keys here as the app grows — no migration required.
/// All keys follow the namespace pattern <c>"domain.setting-name"</c>.
/// </summary>
public static class UserSettingKeys
{
    // ── UI display ────────────────────────────────────────────────────────────
    /// <summary>Date ordering: <c>"dmy"</c> | <c>"mdy"</c> | <c>"ymd"</c>.</summary>
    public const string DateFormat = "ui.date-format";

    /// <summary>Display timezone: empty string = browser local, or <c>"utc7"</c> / <c>"utc0"</c> / <c>"utc-5"</c> / <c>"utc9"</c>.</summary>
    public const string Timezone = "ui.timezone";

    /// <summary>UI colour theme: <c>"dark"</c> | <c>"light"</c> | <c>"system"</c>.</summary>
    public const string Theme = "ui.theme";

    /// <summary>Interface language: <c>"en"</c> | <c>"vi"</c> | <c>"ja"</c>.</summary>
    public const string Language = "ui.language";

    // ── Notifications ─────────────────────────────────────────────────────────
    /// <summary>Whether to send email notifications for project updates: <c>"true"</c> | <c>"false"</c>.</summary>
    public const string NotifyEmail = "notify.email";

    /// <summary>Whether to show browser/in-app push notifications: <c>"true"</c> | <c>"false"</c>.</summary>
    public const string NotifyPush = "notify.push";

    /// <summary>Whether to send an email digest of unread activity: <c>"true"</c> | <c>"false"</c>.</summary>
    public const string NotifyDigest = "notify.digest";

    /// <summary>Whether to notify the user when they are @mentioned in a discussion: <c>"true"</c> | <c>"false"</c>. Independent of the push toggle.</summary>
    public const string NotifyMentions = "notify.mentions";

    // Per-category in-app toggles. Each gates push notifications of one event group,
    // on top of the global NotifyPush master. Absent = enabled (opt-out).
    /// <summary>Work-item events (assigned, unassigned, state changed): <c>"true"</c> | <c>"false"</c>. Default true.</summary>
    public const string NotifyTypeWorkItems = "notify.type.workitems";

    /// <summary>Sprint lifecycle events (activated, closed): <c>"true"</c> | <c>"false"</c>. Default true.</summary>
    public const string NotifyTypeSprints = "notify.type.sprints";

    /// <summary>Membership events (added to / removed from a repository or sprint): <c>"true"</c> | <c>"false"</c>. Default true.</summary>
    public const string NotifyTypeMembership = "notify.type.membership";

    // ── Profile ───────────────────────────────────────────────────────────────
    /// <summary>Public URL of the user's uploaded avatar image. Empty/absent means use avatar colour class.</summary>
    public const string AvatarUrl = "profile.avatar-url";

    // ── Accessibility / UX ────────────────────────────────────────────────────
    /// <summary>Reduce motion / animations: <c>"true"</c> | <c>"false"</c>.</summary>
    public const string ReduceMotion = "a11y.reduce-motion";

    /// <summary>Compact density — tighter row spacing: <c>"true"</c> | <c>"false"</c>.</summary>
    public const string CompactMode = "ui.compact-mode";

    /// <summary>Whether the floating NMate assistant button is shown: <c>"true"</c> / <c>"false"</c>; unset = shown.</summary>
    public const string ShowNMate = "ui.nmate-widget";
}
