using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Notifications.Commands.MarkRead;

/// <summary>Marks one or all notifications as read for the authenticated user.</summary>
/// <param name="NotificationId">When set, marks only this notification; when null, marks all unread.</param>
public sealed record MarkReadCommand(Guid? NotificationId = null) : IRequest<Result>;
