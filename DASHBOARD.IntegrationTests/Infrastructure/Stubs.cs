using System.Collections.Concurrent;
using DASHBOARD.Application.Common.Interfaces;
using MassTransit;

namespace DASHBOARD.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for MassTransit's publisher, keeping every published message so tests can assert on it.
/// </summary>
/// <remarks>
/// Registered as a singleton, so messages accumulate across the whole test run. Assert with
/// <see cref="Messages"/> filtered by type rather than by count.
/// </remarks>
public sealed class RecordingPublishEndpoint : IPublishEndpoint
{
    private readonly ConcurrentBag<object> _messages = [];

    /// <summary>Gets every message published through this endpoint so far.</summary>
    public IReadOnlyCollection<object> Messages => [.. _messages];

    /// <summary>Returns the published messages of one type.</summary>
    /// <typeparam name="T">The message contract.</typeparam>
    /// <returns>All recorded messages assignable to <typeparamref name="T"/>.</returns>
    public IReadOnlyList<T> Published<T>() => [.. _messages.OfType<T>()];

    /// <inheritdoc />
    public Task Publish<T>(T message, CancellationToken ct = default) where T : class
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task Publish<T>(T message, IPipe<PublishContext<T>> pipe, CancellationToken ct = default) where T : class
        => Publish(message, ct);

    /// <inheritdoc />
    public Task Publish<T>(T message, IPipe<PublishContext> pipe, CancellationToken ct = default) where T : class
        => Publish(message, ct);

    /// <inheritdoc />
    public Task Publish(object message, CancellationToken ct = default)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task Publish(object message, IPipe<PublishContext> pipe, CancellationToken ct = default)
        => Publish(message, ct);

    /// <inheritdoc />
    public Task Publish(object message, Type messageType, CancellationToken ct = default)
        => Publish(message, ct);

    /// <inheritdoc />
    public Task Publish(object message, Type messageType, IPipe<PublishContext> pipe, CancellationToken ct = default)
        => Publish(message, ct);

    /// <inheritdoc />
    public Task Publish<T>(object values, CancellationToken ct = default) where T : class
        => Publish(values, ct);

    /// <inheritdoc />
    public Task Publish<T>(object values, IPipe<PublishContext<T>> pipe, CancellationToken ct = default) where T : class
        => Publish(values, ct);

    /// <inheritdoc />
    public Task Publish<T>(object values, IPipe<PublishContext> pipe, CancellationToken ct = default) where T : class
        => Publish(values, ct);

    /// <inheritdoc />
    public ConnectHandle ConnectPublishObserver(IPublishObserver observer) =>
        throw new NotSupportedException("Publish observers are not used in integration tests.");
}

/// <summary>Object storage stub — returns plausible URLs without touching MinIO.</summary>
public sealed class StubStorageService : IStorageService
{
    /// <inheritdoc />
    public Task<string> GenerateUploadUrlAsync(string bucket, string objectKey, TimeSpan expiry, CancellationToken ct)
        => Task.FromResult($"https://storage.test/{bucket}/{objectKey}?upload=1");

    /// <inheritdoc />
    public string GetPublicUrl(string bucket, string objectKey) => $"https://storage.test/{bucket}/{objectKey}";

    /// <inheritdoc />
    public Task DeleteAsync(string bucket, string objectKey, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public Task EnsureBucketExistsAsync(string bucket, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public Task HeadObjectAsync(string bucket, string objectKey, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<string> InitiateMultipartUploadAsync(string bucket, string objectKey, CancellationToken ct)
        => Task.FromResult("stub-upload-id");

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GeneratePartUploadUrlsAsync(
        string bucket, string objectKey, string uploadId, int partCount, TimeSpan expiry, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<string>>(
            [.. Enumerable.Range(1, partCount).Select(i => $"https://storage.test/{bucket}/{objectKey}?part={i}")]);

    /// <inheritdoc />
    public Task CompleteMultipartUploadAsync(
        string bucket, string objectKey, string uploadId,
        IReadOnlyList<(int PartNumber, string ETag)> parts, CancellationToken ct)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task AbortMultipartUploadAsync(string bucket, string objectKey, string uploadId, CancellationToken ct)
        => Task.CompletedTask;
}

/// <summary>HUB chat stub — records calls so tests can assert the API tried to sync.</summary>
public sealed class StubHubChannelService : IHubChannelService
{
    private readonly ConcurrentBag<(Guid ChannelId, Guid UserId)> _added   = [];
    private readonly ConcurrentBag<(Guid ChannelId, Guid UserId)> _removed = [];

    /// <summary>Gets the channel id this stub hands back when a sprint channel is requested.</summary>
    public Guid ChannelId { get; } = Guid.NewGuid();

    /// <summary>Gets the (channel, user) pairs passed to <see cref="AddMemberAsync"/>.</summary>
    public IReadOnlyCollection<(Guid ChannelId, Guid UserId)> Added => [.. _added];

    /// <summary>Gets the (channel, user) pairs passed to <see cref="RemoveMemberAsync"/>.</summary>
    public IReadOnlyCollection<(Guid ChannelId, Guid UserId)> Removed => [.. _removed];

    /// <inheritdoc />
    public Task<Guid?> FindOrCreateSprintChannelAsync(
        Guid workspaceId, Guid sprintId, string sprintName, Guid creatorUserId, CancellationToken ct)
        => Task.FromResult<Guid?>(ChannelId);

    /// <inheritdoc />
    public Task AddMemberAsync(Guid channelId, Guid userId, CancellationToken ct)
    {
        _added.Add((channelId, userId));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveMemberAsync(Guid channelId, Guid userId, CancellationToken ct)
    {
        _removed.Add((channelId, userId));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ArchiveAsync(Guid channelId, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Email stub — records what would have been sent instead of opening an SMTP connection.</summary>
public sealed class StubEmailService : IEmailService
{
    private readonly ConcurrentBag<string> _sentTo = [];

    /// <summary>Gets the addresses an invitation email was sent to.</summary>
    public IReadOnlyCollection<string> SentTo => [.. _sentTo];

    /// <inheritdoc />
    public Task SendInvitationAsync(
        string toEmail, string inviteLink, string invitedByName,
        string repositoryName, int expiryMinutes, CancellationToken ct)
    {
        _sentTo.Add(toEmail);
        return Task.CompletedTask;
    }
}
