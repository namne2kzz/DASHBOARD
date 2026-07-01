using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Domain.Interfaces;

/// <summary>Abstraction over EF Core DbContext exposed to the Application layer.</summary>
public interface IApplicationDbContext
{
    /// <summary>Returns a <see cref="DbSet{TEntity}"/> for the specified entity type.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>A <see cref="DbSet{TEntity}"/> that can be used to query and save instances of <typeparamref name="TEntity"/>.</returns>
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    /// <summary>Persists all pending changes to the database.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
