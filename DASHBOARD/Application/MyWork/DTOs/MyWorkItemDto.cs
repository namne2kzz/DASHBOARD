using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.MyWork.DTOs;

/// <summary>A single work item assigned to the current user, enriched with its owning repository and sprint for cross-repository display.</summary>
/// <param name="Id">The sprint task identifier.</param>
/// <param name="RepositoryId">The owning repository identifier.</param>
/// <param name="RepositoryCode">The repository short code (work-item prefix, e.g. "DASH").</param>
/// <param name="RepositoryName">The human-readable repository name.</param>
/// <param name="SprintId">The sprint the item belongs to, or null for standalone items.</param>
/// <param name="SprintName">The sprint name, or null when not scheduled into a sprint.</param>
/// <param name="WorkItemNumber">Formatted work item number (e.g. "DASH-42").</param>
/// <param name="Type">The work item type.</param>
/// <param name="Title">The work item title.</param>
/// <param name="Priority">The priority level.</param>
/// <param name="State">The current workflow state.</param>
/// <param name="StoryPoints">Story-point estimate (UserStory only).</param>
/// <param name="RemainingWork">Remaining work in hours (Task/Bug).</param>
public sealed record MyWorkItemDto(
    Guid             Id,
    Guid             RepositoryId,
    string           RepositoryCode,
    string           RepositoryName,
    Guid?            SprintId,
    string?          SprintName,
    string           WorkItemNumber,
    SprintTaskType   Type,
    string           Title,
    WorkItemPriority Priority,
    SprintTaskState  State,
    int              StoryPoints,
    decimal          RemainingWork);
