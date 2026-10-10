using DASHBOARD.Domain.Entities;

namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>
/// In-process fan-out bus for real-time in-app notifications.
/// Each active SSE connection registers a channel; the hub pushes to all channels owned by a user.
/// </summary>
public interface INotificationHub
{
    /// <summary>Registers a new SSE connection for <paramref name="userId"/> and returns the reader side of its dedicated channel.</summary>
    /// <param name="userId">The authenticated user.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that yields notifications as they arrive; completes when the connection is removed.</returns>
    IAsyncEnumerable<Notification> Subscribe(Guid userId);

    /// <summary>Removes a previously registered connection channel (called when the HTTP connection closes).</summary>
    /// <param name="userId">The authenticated user.</param>
    /// <param name="channelId">The id returned at subscribe time.</param>
    void Unsubscribe(Guid userId, Guid channelId);

    /// <summary>Pushes a notification to all active connections for <paramref name="userId"/>. No-op if the user is offline.</summary>
    /// <param name="userId">Target user.</param>
    /// <param name="notification">The notification to deliver.</param>
    void Push(Guid userId, Notification notification);
}
