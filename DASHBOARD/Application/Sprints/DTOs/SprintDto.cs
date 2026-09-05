namespace DASHBOARD.Application.Sprints.DTOs;

/// <summary>Sprint snapshot with dates, active status, and optional HUB channel link.</summary>
public sealed record SprintDto(
    Guid      Id,
    Guid      RepositoryId,
    string    Name,
    DateOnly  StartDate,
    DateOnly  EndDate,
    bool      IsActive,
    DateTime  CreatedAt,
    /// <summary>HUB channel ID if a channel was created for this sprint; null otherwise.</summary>
    Guid?     HubChannelId    = null,
    /// <summary>Deep-link URL to open the sprint's HUB channel; null if no channel.</summary>
    string?   HubChannelUrl   = null);

/// <summary>Sprint capacity summary: total hours available vs committed story points.</summary>
public sealed record SprintSummaryDto(
    Guid    SprintId,
    string  SprintName,
    int     TotalWorkingDays,
    decimal TotalCapacityHours,
    int     CommittedStoryPoints,
    int     CompletedStoryPoints,
    int     TotalTasks,
    int     CompletedTasks);
