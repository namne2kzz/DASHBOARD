using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;

namespace DASHBOARD.Infrastructure.Services;

/// <summary>
/// Singleton in-process fan-out hub. One <see cref="Channel{T}"/> per active SSE connection;
/// multiple tabs for the same user each get their own channel so all receive the push.
/// </summary>
internal sealed class NotificationHub : INotificationHub
{
    // userId → { connectionId → channel }
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<Notification>>> _connections = new();

    /// <inheritdoc/>
    public async IAsyncEnumerable<Notification> Subscribe(Guid userId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var connectionId = Guid.NewGuid();
        var channel      = Channel.CreateBounded<Notification>(new BoundedChannelOptions(50)
        {
            FullMode     = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        _connections
            .GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Channel<Notification>>())
            .TryAdd(connectionId, channel);

        try
        {
            // Read manually and swallow the cancellation that fires when the client disconnects —
            // that is the normal end of an SSE stream, not an error. Using ReadAllAsync(ct) would
            // throw OperationCanceledException out of the iterator and surface as an unhandled
            // exception in the debugger on every disconnect.
            while (true)
            {
                Notification notification;
                try
                {
                    if (!await channel.Reader.WaitToReadAsync(ct))
                        break;
                    if (!channel.Reader.TryRead(out notification!))
                        continue;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                yield return notification;
            }
        }
        finally
        {
            Unsubscribe(userId, connectionId);
        }
    }

    // The public interface exposes Subscribe without CancellationToken on the signature;
    // the implementation above accepts it via [EnumeratorCancellation] so callers can pass
    // WithCancellation() on the IAsyncEnumerable.

    /// <inheritdoc/>
    IAsyncEnumerable<Notification> INotificationHub.Subscribe(Guid userId) => Subscribe(userId);

    /// <inheritdoc/>
    public void Unsubscribe(Guid userId, Guid channelId)
    {
        if (!_connections.TryGetValue(userId, out var userChannels)) return;

        if (userChannels.TryRemove(channelId, out var channel))
            channel.Writer.TryComplete();

        if (userChannels.IsEmpty)
            _connections.TryRemove(userId, out _);
    }

    /// <inheritdoc/>
    public void Push(Guid userId, Notification notification)
    {
        if (!_connections.TryGetValue(userId, out var userChannels)) return;

        foreach (var (_, channel) in userChannels)
            channel.Writer.TryWrite(notification);
    }
}
