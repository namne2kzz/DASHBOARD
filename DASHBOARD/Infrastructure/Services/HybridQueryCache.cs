using DASHBOARD.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;

namespace DASHBOARD.Infrastructure.Services;

/// <summary>
/// <see cref="IQueryCache"/> backed by <see cref="HybridCache"/>: an in-process L1 in front of the
/// shared Redis L2 registered in <c>DependencyInjection</c>.
/// </summary>
/// <remarks>
/// The two-level layout is what makes caching worthwhile for hot reads: L1 answers repeat hits without
/// a network round-trip, while L2 keeps the entry alive across app instances and restarts. HybridCache
/// also collapses concurrent misses for one key into a single factory call, which is the stampede
/// protection <see cref="IQueryCache"/> promises.
/// </remarks>
/// <param name="cache">The underlying hybrid cache.</param>
public sealed class HybridQueryCache(HybridCache cache) : IQueryCache
{
    /// <summary>Returns the cached value for a key, invoking the factory once on a miss.</summary>
    /// <typeparam name="T">Cached payload type.</typeparam>
    /// <param name="key">Fully qualified cache key.</param>
    /// <param name="factory">Loader invoked only on a miss.</param>
    /// <param name="tags">Tags this entry belongs to.</param>
    /// <param name="ttl">Optional lifetime override.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cached or freshly loaded value.</returns>
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        IReadOnlyCollection<string> tags,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        // The factory is passed as HybridCache's state argument so the lambda stays static and no
        // closure is allocated per call.
        var options = ttl is null
            ? null
            : new HybridCacheEntryOptions
            {
                Expiration = ttl,
                // Keep L1 well under L2 so a stale local copy cannot outlive an eviction that
                // happened on another instance.
                LocalCacheExpiration = ttl < LocalCap ? ttl : LocalCap,
            };

        return await cache.GetOrCreateAsync(
            key,
            factory,
            static (state, token) => new ValueTask<T>(state(token)),
            options,
            tags,
            ct);
    }

    /// <summary>Evicts every cached entry carrying the given tag.</summary>
    /// <param name="tag">Tag to evict.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once eviction has been requested.</returns>
    public async Task RemoveByTagAsync(string tag, CancellationToken ct = default) =>
        await cache.RemoveByTagAsync(tag, ct);

    /// <summary>Evicts one cached entry by its exact key.</summary>
    /// <param name="key">Fully qualified cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once eviction has been requested.</returns>
    public async Task RemoveAsync(string key, CancellationToken ct = default) =>
        await cache.RemoveAsync(key, ct);

    /// <summary>Upper bound on how long an entry may live in the in-process L1.</summary>
    private static readonly TimeSpan LocalCap = TimeSpan.FromSeconds(20);
}
