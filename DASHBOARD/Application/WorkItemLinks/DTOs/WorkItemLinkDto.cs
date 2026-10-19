namespace DASHBOARD.Application.WorkItemLinks.DTOs;

/// <summary>A work-item link as seen from one work item's perspective.</summary>
/// <param name="Id">Link id.</param>
/// <param name="LinkType">The stored link type name (e.g. "Blocks").</param>
/// <param name="Label">The direction-aware label to show (e.g. "Blocks" or "Blocked by").</param>
/// <param name="TargetTaskId">The other work item's id.</param>
/// <param name="TargetKey">The other work item's formatted key (e.g. "DASH-12").</param>
/// <param name="TargetTitle">The other work item's title.</param>
/// <param name="TargetType">The other work item's type name.</param>
/// <param name="TargetState">The other work item's state name.</param>
/// <param name="Description">Optional note on the link.</param>
public sealed record WorkItemLinkDto(
    Guid    Id,
    string  LinkType,
    string  Label,
    Guid    TargetTaskId,
    string  TargetKey,
    string  TargetTitle,
    string  TargetType,
    string  TargetState,
    string? Description);
