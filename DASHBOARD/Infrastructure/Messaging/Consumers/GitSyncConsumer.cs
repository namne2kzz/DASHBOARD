using DASHBOARD.Application.Common.Caching;
using DASHBOARD.Application.Contracts;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;

namespace DASHBOARD.Infrastructure.Messaging.Consumers;

/// <summary>
/// MassTransit consumer that handles <see cref="GitSyncRequestedEvent"/> by invalidating the cached
/// GitHub overview for the affected connection, so the next Repos-tab load/poll gets a cache miss and
/// re-pulls fresh data from GitHub instead of waiting out the 60s TTL set by
/// <c>GetGitRepositoryOverviewQueryHandler</c>.
/// </summary>
public sealed class GitSyncConsumer(IDistributedCache cache) : IConsumer<GitSyncRequestedEvent>
{
    /// <summary>Removes the cached overview entry matching the event's repository/connection.</summary>
    /// <param name="context">The MassTransit consume context containing the message.</param>
    public async Task Consume(ConsumeContext<GitSyncRequestedEvent> context)
    {
        var msg = context.Message;

        // Built from the shared CacheKeys helper so this invalidation cannot drift away from the key
        // GetGitRepositoryOverviewQueryHandler writes — a mismatch would silently miss and leave the
        // stale entry alive until its TTL expires.
        var cacheKey = CacheKeys.GitOverview(msg.RepositoryId, msg.RepoUrl);

        await cache.RemoveAsync(cacheKey, context.CancellationToken);
    }
}
