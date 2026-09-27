namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>
/// Read-through cache for query handlers, layered in-process (L1) over Redis (L2) and keyed by tags so
/// a mutation can evict every entry it affects in one call.
/// </summary>
/// <remarks>
/// Deliberately abstracts the cache backend away from the Application layer: handlers state what they
/// want cached and under which tags, never which provider stores it. The implementation guarantees that
/// concurrent misses for the same key run the factory once, so a cold key cannot stampede the database.
/// <para>
/// Only cache data the caller is already allowed to see. A handler must run its permission check
/// <em>before</em> calling this — the key is scoped to the resource, not to the caller, so a cached
/// entry is shared by every user permitted to read it.
/// </para>
/// </remarks>
public interface IQueryCache
{
    /// <summary>Returns the cached value for a key, invoking the factory once on a miss.</summary>
    /// <typeparam name="T">Cached payload type; must be serializable.</typeparam>
    /// <param name="key">Fully qualified cache key from <see cref="Caching.CacheKeys"/>.</param>
    /// <param name="factory">Loader invoked only when the key is absent from L1 and L2.</param>
    /// <param name="tags">Tags this entry belongs to, for later bulk eviction.</param>
    /// <param name="ttl">Optional lifetime override; the configured default applies when null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached or freshly loaded value.</returns>
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        IReadOnlyCollection<string> tags,
        TimeSpan? ttl = null,
        CancellationToken ct = default);

    /// <summary>Evicts every cached entry carrying the given tag.</summary>
    /// <param name="tag">Tag to evict, e.g. <see cref="Caching.CacheKeys.RepositoryTag"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once eviction has been requested.</returns>
    Task RemoveByTagAsync(string tag, CancellationToken ct = default);

    /// <summary>Evicts one cached entry by its exact key.</summary>
    /// <remarks>
    /// Prefer <see cref="RemoveByTagAsync"/> when a mutation invalidates a whole group. Use this only
    /// when exactly one entry went stale and evicting its tag would needlessly drop unrelated entries.
    /// </remarks>
    /// <param name="key">Fully qualified cache key from <see cref="Caching.CacheKeys"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once eviction has been requested.</returns>
    Task RemoveAsync(string key, CancellationToken ct = default);
}
