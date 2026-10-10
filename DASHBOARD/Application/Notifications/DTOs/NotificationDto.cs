namespace DASHBOARD.Application.Notifications.DTOs;

/// <summary>Represents a notification shown in the bell dropdown.</summary>
/// <param name="Id">Notification id.</param>
/// <param name="Type">Notification type name (matches <c>NotificationType</c> enum).</param>
/// <param name="Title">Short title.</param>
/// <param name="Body">Supporting detail line.</param>
/// <param name="Url">Optional deep-link.</param>
/// <param name="IsRead">Whether the user has read this notification.</param>
/// <param name="CreatedAt">UTC creation time.</param>
public sealed record NotificationDto(
    Guid      Id,
    string    Type,
    string    Title,
    string    Body,
    string?   Url,
    bool      IsRead,
    DateTime  CreatedAt);
