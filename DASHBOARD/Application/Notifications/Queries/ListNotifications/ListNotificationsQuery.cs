using DASHBOARD.Application.Notifications.DTOs;
using MediatR;

namespace DASHBOARD.Application.Notifications.Queries.ListNotifications;

/// <summary>Returns the 30 most recent notifications for the authenticated user.</summary>
/// <param name="UnreadOnly">When true, returns only unread notifications.</param>
public sealed record ListNotificationsQuery(bool UnreadOnly = false) : IRequest<IReadOnlyList<NotificationDto>>;
