using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DASHBOARD.Infrastructure.Services;

/// <summary>Persists a notification and immediately pushes it to the recipient's active SSE connections.</summary>
internal sealed class NotificationService(
    IApplicationDbContext db,
    IUnitOfWork           uow,
    INotificationHub      hub,
    ILogger<NotificationService> logger) : INotificationService
{
    /// <inheritdoc/>
    public async Task NotifyAsync(
        Guid userId, NotificationType type, string title, string body, string? url, CancellationToken ct)
    {
        try
        {
            var notification = Notification.Create(userId, type, title, body, url);
            db.Set<Notification>().Add(notification);
            await uow.CommitAsync(ct);

            // Push to any open SSE connections — fire-and-forget if offline.
            hub.Push(userId, notification);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deliver notification (type={Type}) to user {UserId}.", type, userId);
        }
    }
}
