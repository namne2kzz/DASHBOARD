namespace DASHBOARD.Application.Notifications.DTOs;

/// <summary>A page of notifications plus the cursor needed to load the next page.</summary>
/// <param name="Items">The notifications in this page, newest first.</param>
/// <param name="HasMore">True when older notifications exist beyond this page.</param>
/// <param name="UnreadCount">Total unread notifications for the user (across all pages).</param>
public sealed record NotificationPageDto(
    IReadOnlyList<NotificationDto> Items,
    bool                           HasMore,
    int                            UnreadCount);
