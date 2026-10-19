using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Controllers.WorkItemLinks.Requests;

/// <summary>Body for <c>POST sprint-tasks/{taskId}/links</c>.</summary>
/// <param name="TargetTaskId">The work item to link to.</param>
/// <param name="LinkType">The relationship type (numeric enum value, matching the rest of the API).</param>
/// <param name="Description">Optional note.</param>
public sealed record CreateWorkItemLinkRequest(
    Guid             TargetTaskId,
    WorkItemLinkType LinkType,
    string?          Description);
