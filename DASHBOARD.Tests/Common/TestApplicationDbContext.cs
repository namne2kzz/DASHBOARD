using DASHBOARD.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Common;

/// <summary>
/// A minimal <see cref="IApplicationDbContext"/> over the EF in-memory provider, for unit-testing handlers.
/// </summary>
/// <remarks>
/// The production <c>ApplicationDbContext</c> is deliberately not reused here. Its model is bound to
/// SQL Server in two ways that the in-memory provider cannot honour: entity configurations declare
/// provider-specific column types (<c>nvarchar(max)</c>), and the <c>HasData</c> seed omits properties
/// that rely on <c>HasDefaultValue</c> (e.g. <c>Repository.IsArchived</c>).
///
/// Those bindings are exactly what integration tests against a real SQL Server must cover, so they are
/// verified there rather than approximated here. This context keeps unit tests fast and focused on
/// handler logic: entities are discovered from the domain assembly, every table starts empty, and the
/// test owns all of its data.
///
/// Soft-delete filtering is reproduced below so handlers that rely on deleted rows being invisible
/// behave the same as in production.
/// </remarks>
public sealed class TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    /// <summary>Registers every domain entity and re-applies the production soft-delete query filter.</summary>
    /// <param name="modelBuilder">The model builder being configured.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply the production entity configurations so relationships match exactly — several
        // foreign keys do not follow EF's naming convention (Invitation.InvitedByUserId → InvitedBy)
        // and would otherwise be discovered as separate shadow properties, leaving Include() empty.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(DASHBOARD.Infrastructure.Persistence.ApplicationDbContext).Assembly);

        foreach (var type in typeof(DASHBOARD.Domain.Common.BaseEntity).Assembly
                     .GetTypes()
                     .Where(t => t is { IsAbstract: false, IsClass: true, Namespace: not null }
                              && t.Namespace!.StartsWith("DASHBOARD.Domain.Entities", StringComparison.Ordinal)
                              && typeof(DASHBOARD.Domain.Common.BaseEntity).IsAssignableFrom(t)))
        {
            modelBuilder.Entity(type);
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Those configurations also carry SQL Server column types such as nvarchar(max),
            // which the in-memory provider rejects. The shape is irrelevant here, so drop them.
            foreach (var property in entityType.GetProperties())
                property.SetColumnType(null);
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType)) continue;

            var param  = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
            var filter = System.Linq.Expressions.Expression.Lambda(
                System.Linq.Expressions.Expression.Not(
                    System.Linq.Expressions.Expression.Property(param, nameof(ISoftDelete.IsDeleted))),
                param);

            entityType.SetQueryFilter(filter);
        }

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>Converts hard deletes into soft deletes, mirroring the production context.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of state entries written.</returns>
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDelete>()
                     .Where(e => e.State == EntityState.Deleted))
        {
            entry.State            = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(ct);
    }
}
