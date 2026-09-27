using DASHBOARD.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;

namespace DASHBOARD.UnitTests.Common;

/// <summary>Creates isolated in-memory databases for unit tests.</summary>
/// <remarks>
/// Each call gets a uniquely-named database, so tests never share state and can run in parallel.
/// See <see cref="TestApplicationDbContext"/> for why the production context is not reused here.
/// </remarks>
public static class TestDbContextFactory
{
    /// <summary>Creates a fresh, empty test database.</summary>
    /// <returns>A disposable handle owning the context and a matching unit of work.</returns>
    public static TestDatabase Create()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // The in-memory provider has no transactions; handlers that open one would otherwise throw.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new TestDatabase(new TestApplicationDbContext(options));
    }
}

/// <summary>Owns a test database's context and unit of work, disposing them together.</summary>
public sealed class TestDatabase : IDisposable
{
    private bool _disposed;

    internal TestDatabase(TestApplicationDbContext db)
    {
        Db  = db;
        Uow = new TestUnitOfWork(db);
    }

    /// <summary>Gets the context under test.</summary>
    public TestApplicationDbContext Db { get; }

    /// <summary>Gets a unit of work bound to the same context.</summary>
    public IUnitOfWork Uow { get; }

    /// <summary>Disposes the underlying context, discarding the in-memory database.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        Db.Dispose();
        _disposed = true;
    }
}

/// <summary>Unit of work over a <see cref="TestApplicationDbContext"/>, mirroring the production one.</summary>
internal sealed class TestUnitOfWork(TestApplicationDbContext context) : IUnitOfWork
{
    /// <summary>Flushes pending changes to the in-memory database.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of state entries written.</returns>
    public Task<int> CommitAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);

    /// <summary>No-op; <see cref="TestDatabase"/> owns the context lifetime.</summary>
    public void Dispose() { }
}
