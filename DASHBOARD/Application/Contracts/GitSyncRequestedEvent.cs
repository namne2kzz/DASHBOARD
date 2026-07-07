namespace DASHBOARD.Application.Contracts;

/// <summary>
/// MassTransit message published by <c>GitHubWebhookController</c> once a GitHub webhook delivery's
/// <c>X-Hub-Signature-256</c> header has been verified and the event type is worth acting on
/// (<c>push</c> or <c>pull_request</c>). Consumed by <c>GitSyncConsumer</c> in Infrastructure, which
/// invalidates the cached overview entry so the next Repos-tab load/poll misses the ~60s cache
/// (<c>GetGitRepositoryOverviewQueryHandler</c>) and re-pulls fresh data from GitHub instead of
/// waiting out the TTL.
/// </summary>
/// <param name="RepositoryId">The project (repository) ID the webhook route was registered against.</param>
/// <param name="RepoUrl">The configured connection's <c>RepoUrl</c> whose cached overview should be invalidated.</param>
public sealed record GitSyncRequestedEvent(Guid RepositoryId, string RepoUrl);
