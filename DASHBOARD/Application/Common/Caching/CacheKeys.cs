namespace DASHBOARD.Application.Common.Caching;

/// <summary>
/// Single source of truth for every Redis cache key this app writes.
/// </summary>
/// <remarks>
/// The <c>dash:</c> prefix is mandatory: the Redis instance in docker-compose is shared with HUB
/// (which uses <c>hub:</c>), so unprefixed keys risk colliding across the two systems. Never build a
/// key with a string literal at the call site — a producer and its invalidating consumer living in
/// different files will silently drift apart and the invalidation stops matching anything.
/// </remarks>
public static class CacheKeys
{
    /// <summary>Key prefix identifying this application inside the shared Redis instance.</summary>
    public const string AppPrefix = "dash";

    /// <summary>Tag grouping every cached entry scoped to one repository.</summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <returns>Tag usable with tag-based cache eviction.</returns>
    public static string RepositoryTag(Guid repositoryId) => $"{AppPrefix}:tag:repo:{repositoryId}";

    /// <summary>Key holding a repository's member list for one optional role filter.</summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="roleFilter">Role name the list was filtered by, or null for the full list.</param>
    /// <returns>Fully qualified cache key.</returns>
    public static string Members(Guid repositoryId, string? roleFilter) =>
        $"{AppPrefix}:members:repo:{repositoryId}:{roleFilter ?? "all"}";

    /// <summary>Key holding a repository's live GitHub overview for one connection.</summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="repoUrl">Git connection URL the overview was fetched from.</param>
    /// <returns>Fully qualified cache key.</returns>
    public static string GitOverview(Guid repositoryId, string repoUrl) =>
        $"{AppPrefix}:git:overview:{repositoryId}:{repoUrl}";
}
