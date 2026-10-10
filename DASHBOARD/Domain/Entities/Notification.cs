using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Domain.Entities;

/// <summary>An in-app notification delivered to a specific user.</summary>
public sealed class Notification : Common.BaseEntity
{
    /// <summary>Gets the recipient user id.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the notification type.</summary>
    public NotificationType Type { get; private set; }

    /// <summary>Gets the short notification title shown in the bell dropdown.</summary>
    public string Title { get; private set; } = default!;

    /// <summary>Gets the supporting detail line (e.g. work item title or sprint name).</summary>
    public string Body { get; private set; } = default!;

    /// <summary>Gets the deep-link URL the user is taken to when they click the notification; null for non-navigable types.</summary>
    public string? Url { get; private set; }

    /// <summary>Gets whether the user has read this notification.</summary>
    public bool IsRead { get; private set; }

    /// <summary>Gets the UTC timestamp when the notification was read; null if unread.</summary>
    public DateTime? ReadAt { get; private set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Recipient user.</summary>
    public User? User { get; private set; }

    // ── Factory ─────────────────────────────────────────────────────────────
    /// <summary>Creates a new notification for a user.</summary>
    /// <param name="userId">Recipient user id.</param>
    /// <param name="type">Notification type.</param>
    /// <param name="title">Short title shown in the dropdown.</param>
    /// <param name="body">Supporting detail line.</param>
    /// <param name="url">Optional deep-link URL.</param>
    public static Notification Create(Guid userId, NotificationType type, string title, string body, string? url = null)
        => new()
        {
            UserId = userId,
            Type   = type,
            Title  = title,
            Body   = body,
            Url    = url,
        };

    /// <summary>Marks the notification as read and stamps <see cref="ReadAt"/>.</summary>
    public void MarkRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
