using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.SprintTasks.DTOs;

/// <summary>Full work item snapshot returned by sprint task queries.</summary>
public sealed record SprintTaskDto(
    Guid              Id,
    Guid?             SprintId,
    Guid              RepositoryId,
    Guid?             BacklogItemId,
    Guid?             ParentId,
    string?           ParentWorkItemNumber,
    string?           ParentTitle,
    string            WorkItemNumber,
    SprintTaskType    Type,
    string            Title,
    string            Description,
    WorkItemPriority  Priority,
    Guid?             AssignedToId,
    string?           AssignedToName,
    string?           AssignedToAvatar,
    SprintTaskState   State,
    int               StoryPoints,
    decimal           OriginalEstimate,
    decimal           RemainingWork,
    decimal           CompletedWork,
    DateTime?         ClosedAt,
    // Bug fields
    string?           StepsToReproduce,
    string?           Environment,
    string?           RootCause,
    string?           Solution,
    string?           Impaction,
    // Task + Bug shared
    string?           UnitTest,
    string?           DesignReview,
    // TestPlan fields
    List<string>?     TestSteps,
    bool?             Automated,
    // Hierarchy
    IReadOnlyList<SprintTaskDto> SubTasks);

/// <summary>Lightweight summary for list views — excludes type-specific fields and sub-task tree.</summary>
public sealed record SprintTaskSummaryDto(
    Guid             Id,
    Guid?            SprintId,
    Guid             RepositoryId,
    string           WorkItemNumber,
    SprintTaskType   Type,
    string           Title,
    WorkItemPriority Priority,
    Guid?            AssignedToId,
    string?          AssignedToName,
    string?          AssignedToAvatar,
    SprintTaskState  State,
    int              StoryPoints,
    decimal          OriginalEstimate,
    decimal          RemainingWork,
    int              SubTaskCount);

/// <summary>Minimal projection for the parent-picker search dropdown.</summary>
/// <param name="Id">Item identifier.</param>
/// <param name="WorkItemNumber">Formatted work item number (e.g. "DASH-3").</param>
/// <param name="Title">Short display title.</param>
public sealed record WorkItemPickerDto(Guid Id, string WorkItemNumber, string Title);
