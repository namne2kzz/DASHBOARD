using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Domain.Entities;

/// <summary>
/// A typed relationship between two work items (e.g. related, blocks, duplicate).
/// Stored once in the source→target direction; the target item shows the inverse label.
/// Parent/child hierarchy is not stored here — see <see cref="SprintTask.ParentId"/>.
/// </summary>
public sealed class WorkItemLink : Common.BaseEntity
{
    /// <summary>Gets or sets the work item the link originates from.</summary>
    public Guid SourceTaskId { get; set; }

    /// <summary>Gets or sets the work item the link points to.</summary>
    public Guid TargetTaskId { get; set; }

    /// <summary>Gets or sets the repository both items belong to (denormalized for isolation).</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the relationship type.</summary>
    public WorkItemLinkType LinkType { get; set; }

    /// <summary>Gets or sets an optional free-text note describing the link.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the user who created the link.</summary>
    public Guid CreatedById { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>The source work item.</summary>
    public SprintTask? SourceTask { get; set; }

    /// <summary>The target work item.</summary>
    public SprintTask? TargetTask { get; set; }

    /// <summary>The user who created the link.</summary>
    public User? CreatedBy { get; set; }
}
