using DASHBOARD.Application.Common.Interfaces;

namespace DASHBOARD.UnitTests.Common;

/// <summary>
/// Pass-through <see cref="IQueryCache"/> for handler tests: always runs the factory and records what
/// was evicted.
/// </summary>
/// <remarks>
/// Never caches. A handler test asserts the query logic it wraps, so serving a second call from a
/// stored value would hide exactly the behaviour under test. Eviction is recorded rather than performed
/// so a test can assert that a mutation invalidated the right tag or key.
/// </remarks>
public sealed class FakeQueryCache : IQueryCache
{
    /// <summary>Tags passed to <see cref="RemoveByTagAsync"/>, in call order.</summary>
    public List<string> EvictedTags { get; } = [];

    /// <summary>Keys passed to <see cref="RemoveAsync"/>, in call order.</summary>
    public List<string> EvictedKeys { get; } = [];

    /// <summary>Invokes the factory and returns its value without storing anything.</summary>
    /// <typeparam name="T">Payload type.</typeparam>
    /// <param name="key">Ignored.</param>
    /// <param name="factory">Loader, always invoked.</param>
    /// <param name="tags">Ignored.</param>
    /// <param name="ttl">Ignored.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whatever the factory produced.</returns>
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        IReadOnlyCollection<string> tags,
        TimeSpan? ttl = null,
        CancellationToken ct = default) => await factory(ct);

    /// <summary>Records the tag as evicted.</summary>
    /// <param name="tag">Tag the handler asked to evict.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task RemoveByTagAsync(string tag, CancellationToken ct = default)
    {
        EvictedTags.Add(tag);
        return Task.CompletedTask;
    }

    /// <summary>Records the key as evicted.</summary>
    /// <param name="key">Key the handler asked to evict.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        EvictedKeys.Add(key);
        return Task.CompletedTask;
    }
}
