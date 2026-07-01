namespace DASHBOARD.Application.Sprints.DTOs;

/// <summary>Sprint snapshot with dates and active status.</summary>
public sealed record SprintDto(
    Guid      Id,
    Guid      RepositoryId,
    string    Name,
    DateOnly  StartDate,
    DateOnly  EndDate,
    bool      IsActive,
    DateTime  CreatedAt);

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
