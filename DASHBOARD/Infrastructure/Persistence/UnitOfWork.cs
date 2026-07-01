using DASHBOARD.Domain.Interfaces;

namespace DASHBOARD.Infrastructure.Persistence;

/// <summary>Wraps <see cref="ApplicationDbContext"/> to implement the Unit of Work pattern, coordinating a single save boundary.</summary>
public sealed class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    private bool _disposed;

    /// <summary>Flushes all pending EF Core change-tracker entries to the database in one round-trip.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public Task<int> CommitAsync(CancellationToken ct = default)
        => context.SaveChangesAsync(ct);

    /// <summary>Disposes the underlying <see cref="ApplicationDbContext"/>. Safe to call multiple times.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        context.Dispose();
        _disposed = true;
    }
}
