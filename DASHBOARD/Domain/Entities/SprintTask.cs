using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;

namespace DASHBOARD.Domain.Entities;

/// <summary>Unified work item entity for sprint execution and standalone Bug/TestPlan tracking.</summary>
public sealed class SprintTask : Common.BaseEntity, ISoftDelete
{
    /// <summary>Gets or sets the sprint this task belongs to. Null for standalone Bug/TestPlan items.</summary>
    public Guid? SprintId { get; set; }

    /// <summary>Gets or sets the repository (project) this item belongs to.</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the optional backlog item this task was promoted from.</summary>
    public Guid? BacklogItemId { get; set; }

    /// <summary>Gets or sets the parent sprint task for sub-task relationships. Null means this is a root-level item.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Gets or sets the per-repository auto-increment sequence number.</summary>
    public int WorkItemNumber { get; set; }

    /// <summary>Builds the display work item number from a repository code and sequence — not persisted, computed at read time.</summary>
    /// <param name="repoCode">Owning repository's short code.</param>
    /// <param name="workItemNumber">Per-repo sequence number.</param>
    /// <returns>Formatted work item number (e.g. "DASH-3").</returns>
    public static string BuildWorkItemNumber(string repoCode, int workItemNumber) => $"{repoCode}-{workItemNumber}";

    /// <summary>Gets or sets the work item type.</summary>
    public SprintTaskType Type { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = default!;

    /// <summary>Gets or sets the detailed description (supports Markdown).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the priority level.</summary>
    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;

    /// <summary>Gets or sets the assigned team member. Null means unassigned. Not applicable for UserStory.</summary>
    public Guid? AssignedToId { get; set; }

    /// <summary>Gets or sets the current state.</summary>
    public SprintTaskState State { get; set; } = SprintTaskState.New;

    /// <summary>Gets or sets the story-point estimate (UserStory only).</summary>
    public int StoryPoints { get; set; }

    /// <summary>Gets or sets the original time estimate in hours (Task/Bug only).</summary>
    public decimal OriginalEstimate { get; set; }

    /// <summary>Gets or sets the remaining work in hours.</summary>
    public decimal RemainingWork { get; set; }

    /// <summary>Gets or sets the logged completed work in hours.</summary>
    public decimal CompletedWork { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the item reached Done state.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the most recent state transition. Null for items created before this field was added.</summary>
    public DateTime? StateChangedAt { get; set; }

    // ── Bug fields ───────────────────────────────────────────────────────────
    /// <summary>Steps to reproduce the issue (Bug only).</summary>
    public string? StepsToReproduce { get; set; }

    /// <summary>Environment where the bug was observed (Bug only).</summary>
    public string? Environment { get; set; }

    /// <summary>Root cause analysis (Bug only).</summary>
    public string? RootCause { get; set; }

    /// <summary>Proposed or applied solution (Bug only).</summary>
    public string? Solution { get; set; }

    /// <summary>Impact description (Bug only).</summary>
    public string? Impaction { get; set; }

    // ── Task + Bug shared fields ─────────────────────────────────────────────
    /// <summary>Unit test notes (Task and Bug only).</summary>
    public string? UnitTest { get; set; }

    /// <summary>Design review notes (Task and Bug only).</summary>
    public string? DesignReview { get; set; }

    // ── TestPlan fields ──────────────────────────────────────────────────────
    /// <summary>Ordered test step descriptions serialized as JSON (TestPlan only).</summary>
    public List<string>? TestSteps { get; set; }

    /// <summary>Whether this test plan is automated (TestPlan only).</summary>
    public bool? Automated { get; set; }

    // ── ISoftDelete ──────────────────────────────────────────────────────────
    /// <inheritdoc/>
    public bool IsDeleted { get; set; }

    /// <inheritdoc/>
    public DateTime? DeletedAt { get; set; }

    /// <inheritdoc/>
    public Guid? DeletedByUserId { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    /// <summary>Parent sprint. Null for standalone items.</summary>
    public Sprint? Sprint { get; set; }

    /// <summary>Source backlog item. Null when not promoted from backlog.</summary>
    public BacklogItem? BacklogItem { get; set; }

    /// <summary>Parent task for sub-tasks. Null for root items.</summary>
    public SprintTask? Parent { get; set; }

    /// <summary>Child sub-tasks.</summary>
    public ICollection<SprintTask> SubTasks { get; set; } = [];

    /// <summary>Assigned team member.</summary>
    public User? AssignedTo { get; set; }

    /// <summary>Discussion thread entries.</summary>
    public ICollection<DiscussionEntry> Discussions { get; set; } = [];
}
