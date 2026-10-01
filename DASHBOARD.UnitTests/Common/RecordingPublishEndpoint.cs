using MassTransit;

namespace DASHBOARD.UnitTests.Common;

/// <summary>
/// <see cref="IPublishEndpoint"/> stand-in for handler tests: records what was published instead of
/// touching a broker.
/// </summary>
/// <remarks>
/// Only the <c>Publish&lt;T&gt;(T, CancellationToken)</c> overloads are used by handlers, so the rest
/// of the interface throws rather than silently swallowing a call a test meant to assert on.
/// </remarks>
public sealed class RecordingPublishEndpoint : IPublishEndpoint
{
    private readonly List<object> _published = [];

    /// <summary>Everything published through this endpoint, in call order.</summary>
    public IReadOnlyList<object> Published => _published;

    /// <summary>Returns the published messages of one type.</summary>
    /// <typeparam name="T">Message type to filter by.</typeparam>
    /// <returns>Matching messages, in call order.</returns>
    public IReadOnlyList<T> PublishedOf<T>() => _published.OfType<T>().ToList();

    /// <inheritdoc />
    public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        _published.Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task Publish(object message, CancellationToken cancellationToken = default)
    {
        _published.Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class =>
        Publish(message, cancellationToken);

    /// <inheritdoc />
    public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class =>
        Publish(message, cancellationToken);

    /// <inheritdoc />
    public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) =>
        Publish(message, cancellationToken);

    /// <inheritdoc />
    public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default) =>
        Publish(message, cancellationToken);

    /// <inheritdoc />
    public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) =>
        Publish(message, cancellationToken);

    /// <inheritdoc />
    public Task Publish<T>(object values, CancellationToken cancellationToken = default) where T : class =>
        throw new NotSupportedException("Message-initializer publishing is not used by these handlers.");

    /// <inheritdoc />
    public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default) where T : class =>
        throw new NotSupportedException("Message-initializer publishing is not used by these handlers.");

    /// <inheritdoc />
    public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) where T : class =>
        throw new NotSupportedException("Message-initializer publishing is not used by these handlers.");

    /// <inheritdoc />
    public ConnectHandle ConnectPublishObserver(IPublishObserver observer) =>
        throw new NotSupportedException("Observers are not used by these handlers.");
}
