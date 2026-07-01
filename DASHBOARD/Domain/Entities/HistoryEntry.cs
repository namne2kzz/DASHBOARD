namespace DASHBOARD.Domain.Entities;

/// <summary>Immutable audit-trail entry recording a change to a <see cref="SprintTask"/>.</summary>
public sealed class HistoryEntry : Common.BaseEntity
{
    /// <summary>Gets or sets the parent sprint task (work item).</summary>
    public Guid SprintTaskId { get; set; }

    /// <summary>Gets or sets the repository this history entry belongs to (denormalized for query isolation).</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the user who made the change.</summary>
    public Guid AuthorId { get; set; }

    /// <summary>Gets or sets a human-readable description of what changed (e.g. "State changed from Todo to Active").</summary>
    public string Message { get; set; } = default!;

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Parent sprint task.</summary>
    public SprintTask? SprintTask { get; set; }

    /// <summary>User who triggered the change.</summary>
    public User? Author { get; set; }
}
