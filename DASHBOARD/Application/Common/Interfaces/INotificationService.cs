using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>
/// Persists a notification to the database AND pushes it in real-time to any active SSE
/// connections for the recipient. Use this from command handlers — never call
/// <see cref="INotificationHub"/> directly from the Application layer.
/// </summary>
public interface INotificationService
{
    /// <summary>Creates, persists, and delivers a notification to <paramref name="userId"/>.</summary>
    /// <param name="userId">Recipient user id.</param>
    /// <param name="type">Notification type.</param>
    /// <param name="title">Short title shown in the bell dropdown.</param>
    /// <param name="body">Supporting detail line.</param>
    /// <param name="url">Optional deep-link the user is taken to on click.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyAsync(Guid userId, NotificationType type, string title, string body, string? url, CancellationToken ct);
}
