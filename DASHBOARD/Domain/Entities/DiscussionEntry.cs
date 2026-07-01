using DASHBOARD.Domain.Interfaces;

namespace DASHBOARD.Domain.Entities;

/// <summary>A single comment in the discussion thread of a <see cref="SprintTask"/>.</summary>
public sealed class DiscussionEntry : Common.BaseEntity, ISoftDelete
{
    /// <summary>Gets or sets the parent sprint task (work item).</summary>
    public Guid SprintTaskId { get; set; }

    /// <summary>Gets or sets the repository this discussion entry belongs to (denormalized for query isolation).</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the user who posted the comment.</summary>
    public Guid AuthorId { get; set; }

    /// <summary>Gets or sets the comment body (Markdown supported).</summary>
    public string Body { get; set; } = default!;

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Parent sprint task.</summary>
    public SprintTask? SprintTask { get; set; }

    /// <summary>Comment author.</summary>
    public User? Author { get; set; }

    // ── ISoftDelete ──────────────────────────────────────────────────────────
    /// <inheritdoc/>
    public bool IsDeleted { get; set; }

    /// <inheritdoc/>
    public DateTime? DeletedAt { get; set; }

    /// <inheritdoc/>
    public Guid? DeletedByUserId { get; set; }
}
