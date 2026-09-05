using System.Linq.Expressions;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Infrastructure.Persistence;

/// <summary>EF Core database context for the DASHBOARD application.</summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    // ── Identity & Access ────────────────────────────────────────────────────
    /// <summary>Organizations — top-level tenants owning users and repositories.</summary>
    public DbSet<Organization> Organizations => Set<Organization>();

    /// <summary>Application users.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Issued JWTs tracked for server-side revocation.</summary>
    public DbSet<UserToken> UserTokens => Set<UserToken>();

    /// <summary>Repository invitations sent to external email addresses.</summary>
    public DbSet<Invitation> Invitations => Set<Invitation>();

    /// <summary>Project repositories (Jira-style projects).</summary>
    public DbSet<Repository> Repositories => Set<Repository>();

    /// <summary>Repository membership and role assignments.</summary>
    public DbSet<RepositoryMember> RepositoryMembers => Set<RepositoryMember>();

    /// <summary>Roles with permission sets — global default roles and repository-scoped custom roles.</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>Repository-level metadata catalogs (selectable values per key, e.g. available versions).</summary>
    public DbSet<RepositoryMetadata> RepositoryMetadata => Set<RepositoryMetadata>();

    // ── Work Items (now unified under SprintTasks) ──────────────────────────
    /// <summary>Discussion comments on sprint tasks.</summary>
    public DbSet<DiscussionEntry> DiscussionEntries => Set<DiscussionEntry>();

    /// <summary>Audit history entries for sprint tasks.</summary>
    public DbSet<HistoryEntry> HistoryEntries => Set<HistoryEntry>();

    /// <summary>Work item ↔ metadata catalog assignments (labels, components, versions).</summary>
    public DbSet<WorkItemMetadata> WorkItemMetadata => Set<WorkItemMetadata>();

    // ── Backlog ──────────────────────────────────────────────────────────────
    /// <summary>Product backlog items (Epic / Feature / UserStory hierarchy).</summary>
    public DbSet<BacklogItem> BacklogItems => Set<BacklogItem>();

    // ── Sprint Planning ───────────────────────────────────────────────────────
    /// <summary>Sprints / iterations.</summary>
    public DbSet<Sprint> Sprints => Set<Sprint>();

    /// <summary>Team member capacity rows per sprint.</summary>
    public DbSet<CapacityMember> CapacityMembers => Set<CapacityMember>();

    /// <summary>Day-off entries per sprint (personal or team-wide).</summary>
    public DbSet<DayOff> DaysOff => Set<DayOff>();

    /// <summary>Tasks and user stories committed to a sprint.</summary>
    public DbSet<SprintTask> SprintTasks => Set<SprintTask>();

    // ── Smart Board ───────────────────────────────────────────────────────────
    /// <summary>WIP-limited kanban board columns.</summary>
    public DbSet<SmartBoardColumn> SmartBoardColumns => Set<SmartBoardColumn>();

    // ─────────────────────────────────────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ApplySoftDeleteFilters(modelBuilder);
        SeedData.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Converts hard-delete operations into soft-deletes for any entity that implements <see cref="ISoftDelete"/>.
    /// Sets <see cref="ISoftDelete.IsDeleted"/> and <see cref="ISoftDelete.DeletedAt"/> automatically.
    /// <see cref="ISoftDelete.DeletedByUserId"/> should be set by the Application layer before calling this.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of state entries written to the database.</returns>
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDelete>()
                     .Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Registers a <c>WHERE IsDeleted = 0</c> global query filter on every entity type that implements
    /// <see cref="ISoftDelete"/>. TPT child types (e.g. <see cref="BugWorkItem"/>) inherit the filter
    /// from <see cref="WorkItem"/> automatically — only root types are processed to avoid duplicate predicates.
    /// </summary>
    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType)) continue;

            var param  = Expression.Parameter(entityType.ClrType, "e");
            var filter = Expression.Lambda(
                Expression.Not(Expression.Property(param, nameof(ISoftDelete.IsDeleted))),
                param);

            entityType.SetQueryFilter(filter);
        }
    }
}
