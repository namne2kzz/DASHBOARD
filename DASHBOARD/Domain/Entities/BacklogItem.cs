using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Domain.Entities;

/// <summary>A planning artifact in the product backlog. Supports the Epic → Feature → UserStory hierarchy via <see cref="ParentId"/>.</summary>
public sealed class BacklogItem : Common.BaseEntity
{
    /// <summary>Gets or sets the owning repository.</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the hierarchy level (Epic / Feature / UserStory).</summary>
    public BacklogItemType Type { get; set; }

    /// <summary>Gets or sets the item title.</summary>
    public string Title { get; set; } = default!;

    /// <summary>Gets or sets the parent item ID for hierarchy. Null means this is a root-level item.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the fractional ordering rank within the same level.
    /// When reordering, new rank = (prev_rank + next_rank) / 2.
    /// Re-normalize when gap falls below 0.001.
    /// </summary>
    public decimal Rank { get; set; }

    /// <summary>Gets or sets the sprint this item is tentatively planned for. Null means unscheduled.</summary>
    public Guid? SprintId { get; set; }

    /// <summary>Gets or sets the refinement lifecycle state.</summary>
    public BacklogItemState State { get; set; }

    /// <summary>Gets or sets the Fibonacci story-point estimate. Null means unestimated.</summary>
    public int? StoryPoints { get; set; }

    /// <summary>Gets or sets the T-shirt size estimate. Null means unestimated.</summary>
    public TshirtSize? TshirtSize { get; set; }

    /// <summary>Gets or sets acceptance criteria for this backlog item.</summary>
    public string AcceptanceCriteria { get; set; } = string.Empty;

    /// <summary>Gets or sets a list of refinement document titles or URLs. Serialized as JSON.</summary>
    public List<string> Documents { get; set; } = [];

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Sprint this item is tentatively planned for.</summary>
    public Sprint? Sprint { get; set; }

    /// <summary>Parent backlog item. Null for root-level items.</summary>
    public BacklogItem? Parent { get; set; }

    /// <summary>Child backlog items.</summary>
    public ICollection<BacklogItem> Children { get; set; } = [];
}
