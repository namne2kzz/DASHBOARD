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

    /// <summary>
    /// Gets or sets the root comment this entry replies to, or null when it is itself a root comment.
    /// </summary>
    /// <remarks>
    /// Always points at a root entry, never at another reply: the thread is deliberately two levels
    /// deep, matching the one-level indent the UI draws. A reply to a reply is re-parented to the
    /// same root and names its addressee with an @mention in the body instead of nesting deeper.
    /// </remarks>
    public Guid? ParentId { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Parent sprint task.</summary>
    public SprintTask? SprintTask { get; set; }

    /// <summary>Comment author.</summary>
    public User? Author { get; set; }

    /// <summary>Root comment this entry replies to, when it is a reply.</summary>
    public DiscussionEntry? Parent { get; set; }

    /// <summary>Replies made to this comment; empty for entries that are themselves replies.</summary>
    public ICollection<DiscussionEntry> Replies { get; set; } = [];

    // ── ISoftDelete ──────────────────────────────────────────────────────────
    /// <inheritdoc/>
    public bool IsDeleted { get; set; }

    /// <inheritdoc/>
    public DateTime? DeletedAt { get; set; }

    /// <inheritdoc/>
    public Guid? DeletedByUserId { get; set; }
}
