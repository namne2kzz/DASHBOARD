using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.SprintTasks.DTOs;

/// <summary>Flat work-item projection for the kanban board — no tree hierarchy.</summary>
/// <param name="Id">Item identifier.</param>
/// <param name="WorkItemNumber">Display ID computed as "{RepositoryCode}-{sequence}" (e.g. "DASH-3").</param>
/// <param name="Type">Work-item type (Task / Bug / TestPlan / UserStory).</param>
/// <param name="Title">Short display title.</param>
/// <param name="Priority">Priority level.</param>
/// <param name="AssignedToId">Assignee user ID; null when unassigned.</param>
/// <param name="AssignedToName">Assignee display name; null when unassigned.</param>
/// <param name="AssignedToAvatar">Assignee avatar CSS class; null when unassigned.</param>
/// <param name="State">Current workflow state.</param>
/// <param name="OriginalEstimate">Original time estimate in hours.</param>
/// <param name="RemainingWork">Remaining hours estimate.</param>
/// <param name="CompletedWork">Logged completed hours.</param>
/// <param name="StateChangedAt">UTC timestamp of the last state transition; null for legacy items.</param>
/// <param name="Labels">Assigned "Labels" catalog values, for card chips.</param>
public sealed record BoardTaskDto(
    Guid             Id,
    string           WorkItemNumber,
    SprintTaskType   Type,
    string           Title,
    WorkItemPriority Priority,
    Guid?            AssignedToId,
    string?          AssignedToName,
    string?          AssignedToAvatar,
    WorkItemState    State,
    decimal          OriginalEstimate,
    decimal          RemainingWork,
    decimal          CompletedWork,
    DateTime?        StateChangedAt,
    IReadOnlyList<string> Labels);
