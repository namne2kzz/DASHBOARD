namespace DASHBOARD.Application.Overview.DTOs;

/// <summary>Aggregated overview statistics for a repository, optionally scoped to a sprint.</summary>
/// <param name="WorkItemCounts">Counts of work items by type for the selected sprint.</param>
/// <param name="StatusDistribution">Counts of work items by workflow state for the selected sprint.</param>
/// <param name="TypeDistribution">Per-type breakdown with display name and count.</param>
/// <param name="AvailableSprints">All repository sprints ordered by start date descending, for the sprint selector.</param>
/// <param name="BurndownData">Ideal and actual story-point burndown arrays aligned to sprint working days.</param>
/// <param name="StoryPoints">Committed vs completed story points for the selected sprint.</param>
/// <param name="VelocityTrend">Per-sprint velocity across the five most recent sprints for the trend chart.</param>
public sealed record OverviewStatsDto(
    WorkItemCountsDto WorkItemCounts,
    StatusDistributionDto StatusDistribution,
    IReadOnlyList<TypeDistributionItemDto> TypeDistribution,
    IReadOnlyList<SprintRefDto> AvailableSprints,
    BurndownDataDto BurndownData,
    StoryPointsDto StoryPoints,
    IReadOnlyList<VelocityTrendItemDto> VelocityTrend);

/// <summary>Counts of work items by type for the selected sprint.</summary>
/// <param name="Total">Total item count.</param>
/// <param name="Bugs">Bug count.</param>
/// <param name="UserStories">User story count.</param>
/// <param name="Tasks">Task count.</param>
/// <param name="TestPlans">Test plan count.</param>
public sealed record WorkItemCountsDto(
    int Total,
    int Bugs,
    int UserStories,
    int Tasks,
    int TestPlans);

/// <summary>Item counts per workflow state for the selected sprint.</summary>
/// <param name="Todo">Items in Todo state.</param>
/// <param name="Active">Items actively being worked on.</param>
/// <param name="InReview">Items awaiting code/design review.</param>
/// <param name="Done">Completed items.</param>
/// <param name="New">Standalone items not yet committed to a sprint.</param>
/// <param name="Backlog">Items in the backlog state.</param>
public sealed record StatusDistributionDto(
    int Todo,
    int Active,
    int InReview,
    int Done,
    int New,
    int Backlog);

/// <summary>Work item count for a single type.</summary>
/// <param name="Type"><see cref="DASHBOARD.Domain.Enums.SprintTaskType"/> name.</param>
/// <param name="Count">Number of items with this type in the selected sprint.</param>
public sealed record TypeDistributionItemDto(string Type, int Count);

/// <summary>Sprint identifier and display name for the sprint selector dropdown.</summary>
/// <param name="Id">Sprint primary key.</param>
/// <param name="Name">Sprint display name (e.g. "Sprint 1 — May 2026").</param>
public sealed record SprintRefDto(Guid Id, string Name);

/// <summary>Burndown chart data — one element per sprint working day.</summary>
/// <param name="Ideal">Linear burn-down from committed story points to zero.</param>
/// <param name="Actual">Actual remaining story points per working day, derived from task <c>ClosedAt</c> timestamps. Truncated at today for future days.</param>
public sealed record BurndownDataDto(
    IReadOnlyList<double> Ideal,
    IReadOnlyList<double> Actual);

/// <summary>Story-point totals for the selected sprint.</summary>
/// <param name="Committed">Total story points on all tasks in the sprint.</param>
/// <param name="Completed">Story points on tasks in Done state.</param>
public sealed record StoryPointsDto(int Committed, int Completed);

/// <summary>Velocity entry for one sprint used in the trend chart.</summary>
/// <param name="SprintName">Sprint display name.</param>
/// <param name="CommittedPoints">Total story points committed in this sprint.</param>
/// <param name="CompletedPoints">Story points completed (Done state) in this sprint.</param>
public sealed record VelocityTrendItemDto(
    string SprintName,
    int CommittedPoints,
    int CompletedPoints);
