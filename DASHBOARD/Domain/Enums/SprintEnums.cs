namespace DASHBOARD.Domain.Enums;

/// <summary>Lifecycle state of a sprint.</summary>
public enum SprintStatus
{
    /// <summary>Sprint created but not yet started.</summary>
    Planning = 0,
    /// <summary>Sprint is currently running.</summary>
    Active   = 1,
    /// <summary>Sprint has been closed.</summary>
    Closed   = 2,
}

/// <summary>Type classification of a sprint work item.</summary>
public enum SprintTaskType
{
    UserStory = 0,
    Task      = 1,
    Bug       = 2,
    TestPlan  = 3,
}

/// <summary>
/// Progress state of a sprint work item. New is used for standalone items (Bug/TestPlan) not yet assigned to a sprint.
/// </summary>
/// <remarks>Replaced by <see cref="WorkItemState"/>. Retained so existing EF migrations and DB snapshots compile.</remarks>
[Obsolete("Use WorkItemState instead. This enum is kept only to keep EF migration history compilable.")]
public enum SprintTaskState
{
    New      = 0,
    Backlog  = 1,
    Todo     = 2,
    Active   = 3,
    InReview = 4,
    Done     = 5,
}

/// <summary>Lifecycle state of a work item. Valid states differ by type — see <see cref="StateCategory"/> for terminal grouping.</summary>
public enum WorkItemState
{
    // ── Open (starting state for all types) ──────────────────────────────────
    /// <summary>New/unstarted — UserStory, Bug, TestPlan default.</summary>
    Open       = 0,
    /// <summary>Queued, planned — Task default.</summary>
    ToDo       = 1,
    // ── In progress ──────────────────────────────────────────────────────────
    /// <summary>Active development (all types).</summary>
    InProgress = 2,
    /// <summary>Code review / UAT — Task, Bug.</summary>
    InReview   = 3,
    /// <summary>QA confirmed fix — Bug only.</summary>
    Verified   = 4,
    /// <summary>Test execution in progress — TestPlan only.</summary>
    Running    = 5,
    // ── Terminal ─────────────────────────────────────────────────────────────
    /// <summary>Completed — UserStory, Task.</summary>
    Done       = 6,
    /// <summary>Test passed — TestPlan only.</summary>
    Passed     = 7,
    /// <summary>Test failed — TestPlan only.</summary>
    Failed     = 8,
    /// <summary>Abandoned / won't fix — any type.</summary>
    Closed     = 9,
}

/// <summary>Macro category grouping <see cref="WorkItemState"/> for system logic (e.g. sprint close check).</summary>
public enum StateCategory
{
    /// <summary>Item has not been started.</summary>
    ToDo,
    /// <summary>Item is being actively worked on.</summary>
    InProgress,
    /// <summary>Item has reached a terminal state.</summary>
    Done,
}

/// <summary>Priority level for a work item.</summary>
public enum WorkItemPriority { Low = 0, Medium = 1, High = 2, Critical = 3 }
