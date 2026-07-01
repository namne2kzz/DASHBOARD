namespace DASHBOARD.Domain.Enums;

/// <summary>Type classification of a sprint work item.</summary>
public enum SprintTaskType
{
    UserStory = 0,
    Task      = 1,
    Bug       = 2,
    TestPlan  = 3,
}

/// <summary>Progress state of a sprint work item. New is used for standalone items (Bug/TestPlan) not yet assigned to a sprint.</summary>
public enum SprintTaskState
{
    New      = 0,
    Backlog  = 1,
    Todo     = 2,
    Active   = 3,
    InReview = 4,
    Done     = 5,
}

/// <summary>Priority level for a work item.</summary>
public enum WorkItemPriority { Low = 0, Medium = 1, High = 2, Critical = 3 }
