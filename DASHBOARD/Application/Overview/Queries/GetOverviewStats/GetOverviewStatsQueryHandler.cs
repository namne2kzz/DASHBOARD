using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Overview.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Overview.Queries.GetOverviewStats;

/// <summary>Handles <see cref="GetOverviewStatsQuery"/> — aggregates sprint statistics in three focused EF Core queries.</summary>
public sealed class GetOverviewStatsQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetOverviewStatsQuery, OverviewStatsDto>
{
    /// <summary>Loads sprints, sprint tasks, and cross-sprint velocity data then returns all computed metrics.</summary>
    /// <param name="query">The overview query with optional sprint scope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="OverviewStatsDto"/> with all computed dashboard metrics.</returns>
    public async Task<OverviewStatsDto> Handle(GetOverviewStatsQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var sprints = await db.Set<Sprint>().AsNoTracking()
            .Where(s => s.RepositoryId == query.RepositoryId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);

        var selectedSprint = query.SprintId.HasValue
            ? sprints.FirstOrDefault(s => s.Id == query.SprintId.Value)
              ?? throw new NotFoundException(nameof(Sprint), query.SprintId.Value)
            : SelectDefaultSprint(sprints);

        List<SprintTask> tasks = selectedSprint is null
            ? []
            : await db.Set<SprintTask>().AsNoTracking()
                .Include(t => t.AssignedTo)
                .Where(t => t.SprintId == selectedSprint.Id && !t.IsDeleted)
                .ToListAsync(ct);

        var availableSprints = sprints
            .Select(s => new SprintRefDto(s.Id, s.Name, s.StartDate, s.EndDate))
            .ToList();

        var counts = new WorkItemCountsDto(
            tasks.Count,
            tasks.Count(t => t.Type == SprintTaskType.Bug),
            tasks.Count(t => t.Type == SprintTaskType.UserStory),
            tasks.Count(t => t.Type == SprintTaskType.Task),
            tasks.Count(t => t.Type == SprintTaskType.TestPlan));

        var statusDist = new StatusDistributionDto(
            tasks.Count(t => t.State == WorkItemState.ToDo),
            tasks.Count(t => t.State == WorkItemState.InProgress),
            tasks.Count(t => t.State == WorkItemState.InReview),
            tasks.Count(t => t.Category == StateCategory.Done),
            tasks.Count(t => t.State == WorkItemState.Open),
            tasks.Count(t => t.State == WorkItemState.Open));

        var typeDist = Enum.GetValues<SprintTaskType>()
            .Select(type => new TypeDistributionItemDto(
                type.ToString(),
                tasks.Count(t => t.Type == type)))
            .ToList();

        var storyPoints = new StoryPointsDto(
            tasks.Sum(t => t.StoryPoints),
            tasks.Where(t => t.Category == StateCategory.Done).Sum(t => t.StoryPoints));

        var burndown = selectedSprint is null
            ? new BurndownDataDto([], [])
            : ComputeBurndown(selectedSprint, tasks);

        // Velocity trend: single aggregation query over the 5 most recent sprints.
        var recentSprintIds = sprints.Take(5).Select(s => s.Id).ToHashSet();
        var velocityRaw = await db.Set<SprintTask>().AsNoTracking()
            .Where(t => t.SprintId.HasValue && recentSprintIds.Contains(t.SprintId!.Value) && !t.IsDeleted)
            .GroupBy(t => t.SprintId!.Value)
            .Select(g => new
            {
                SprintId  = g.Key,
                Committed = g.Sum(t => t.StoryPoints),
                // Category is computed — compare underlying int values EF Core can translate.
                // Done=6, Passed=7, Failed=8, Closed=9 are all terminal (StateCategory.Done).
                Completed = g.Sum(t => (int)t.State >= 6 ? t.StoryPoints : 0),
            })
            .ToListAsync(ct);

        var velocityLookup = velocityRaw.ToDictionary(v => v.SprintId);
        var velocityTrend = sprints
            .Take(5)
            .OrderBy(s => s.StartDate)
            .Select(s => velocityLookup.TryGetValue(s.Id, out var v)
                ? new VelocityTrendItemDto(s.Name, v.Committed, v.Completed)
                : new VelocityTrendItemDto(s.Name, 0, 0))
            .ToList();

        // Type trend: selected sprint plus up to six preceding sprints, oldest first.
        var typeTrend = new List<SprintTypeTrendItemDto>();
        if (selectedSprint is not null)
        {
            var selectedIndex = sprints.FindIndex(s => s.Id == selectedSprint.Id);
            var trendSprints = sprints.Skip(selectedIndex).Take(7).OrderBy(s => s.StartDate).ToList();
            var trendSprintIds = trendSprints.Select(s => s.Id).ToHashSet();

            var typeTrendRaw = await db.Set<SprintTask>().AsNoTracking()
                .Where(t => t.SprintId.HasValue && trendSprintIds.Contains(t.SprintId!.Value) && !t.IsDeleted)
                .GroupBy(t => t.SprintId!.Value)
                .Select(g => new
                {
                    SprintId = g.Key,
                    UserStory = g.Count(t => t.Type == SprintTaskType.UserStory),
                    Task = g.Count(t => t.Type == SprintTaskType.Task),
                    Bug = g.Count(t => t.Type == SprintTaskType.Bug),
                    TestPlan = g.Count(t => t.Type == SprintTaskType.TestPlan),
                })
                .ToListAsync(ct);

            var typeTrendLookup = typeTrendRaw.ToDictionary(v => v.SprintId);
            typeTrend = trendSprints
                .Select(s => typeTrendLookup.TryGetValue(s.Id, out var v)
                    ? new SprintTypeTrendItemDto(s.Name, v.UserStory, v.Task, v.Bug, v.TestPlan)
                    : new SprintTypeTrendItemDto(s.Name, 0, 0, 0, 0))
                .ToList();
        }

        var sprintHealth = selectedSprint is null
            ? new SprintHealthDto("OnTrack", 0, 0, 0)
            : ComputeSprintHealth(selectedSprint, burndown);

        var cycleTime = ComputeCycleTime(tasks);

        var assigneeWorkload = tasks
            .GroupBy(t => t.AssignedToId)
            .Select(g => new AssigneeWorkloadItemDto(
                g.Key,
                g.Key.HasValue ? g.First().AssignedTo?.Name ?? "Unassigned" : "Unassigned",
                g.Key.HasValue ? g.First().AssignedTo?.AvatarClass : null,
                g.Count(),
                g.Sum(t => t.StoryPoints)))
            .OrderByDescending(a => a.TaskCount)
            .ToList();

        var recentActivity = await db.Set<HistoryEntry>().AsNoTracking()
            .Include(h => h.Author)
            .Include(h => h.SprintTask)
            .Where(h => h.RepositoryId == query.RepositoryId)
            .OrderByDescending(h => h.CreatedAt)
            .Take(8)
            .Select(h => new RecentActivityItemDto(
                h.SprintTaskId,
                h.SprintTask!.Title,
                h.Message,
                h.Author!.Name,
                h.Author.AvatarClass,
                h.CreatedAt))
            .ToListAsync(ct);

        var report = ComputeReport(selectedSprint, tasks, storyPoints);

        // Board columns carry the WIP and aging limits the attention strip judges against.
        var columns = await db.Set<SmartBoardColumn>().AsNoTracking()
            .Where(c => c.RepositoryId == query.RepositoryId)
            .OrderBy(c => c.Order)
            .ToListAsync(ct);

        // The repository's short code turns a work item number into the "DASH-22" people recognise.
        var repoCode = await db.Set<Repository>().AsNoTracking()
            .Where(r => r.Id == query.RepositoryId)
            .Select(r => r.Code)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var attention  = BuildAttention(tasks, columns, assigneeWorkload, repoCode);
        var comparison = await ComputeComparisonAsync(sprints, selectedSprint, ct);

        return new OverviewStatsDto(
            counts,
            statusDist,
            typeDist,
            availableSprints,
            burndown,
            storyPoints,
            velocityTrend,
            typeTrend,
            sprintHealth,
            cycleTime,
            assigneeWorkload,
            recentActivity,
            report,
            attention,
            comparison);
    }

    /// <summary>Points per person per sprint above which the workload is called overloaded.</summary>
    /// <remarks>
    /// A flat threshold, not a capacity lookup: the capacity screen models real availability
    /// (days off, part-time) and this strip must not disagree with it. It flags only what is
    /// lopsided enough to be worth a second look either way.
    /// </remarks>
    private const int OverloadPointThreshold = 20;

    /// <summary>Collects the problems on the board that warrant action, most severe first.</summary>
    /// <param name="tasks">The selected sprint's non-deleted tasks.</param>
    /// <param name="columns">Board columns, carrying the WIP and aging limits to judge against.</param>
    /// <param name="workload">Already-computed per-assignee workload.</param>
    /// <param name="repoCode">Repository short code, for rendering work item numbers.</param>
    /// <returns>At most five items; an empty list when nothing needs attention.</returns>
    private static List<AttentionItemDto> BuildAttention(
        List<SprintTask>                   tasks,
        List<SmartBoardColumn>             columns,
        List<AssigneeWorkloadItemDto>      workload,
        string                             repoCode)
    {
        var items = new List<AttentionItemDto>();
        var now   = DateTime.UtcNow;

        // Columns over their WIP limit. A limit of 0 means the column is unbounded.
        //
        // The count is per state, which is also how the board itself fills its columns: two
        // columns mapped to the same state show the same cards, so both are genuinely over
        // the limit when that state is. Each is still reported by its own name, since that
        // is the name the reader will be looking for on the board.
        foreach (var column in columns.Where(c => c.WipLimit > 0))
        {
            var count = tasks.Count(t => t.State == column.MappedState);
            if (count <= column.WipLimit) continue;

            items.Add(new AttentionItemDto(
                "WipExceeded",
                column.WipMode == WipMode.Hard ? "Critical" : "Warning",
                $"{column.Name} over WIP · {count}/{column.WipLimit}",
                "boards",
                column.Id));
        }

        // People carrying noticeably more than the rest.
        foreach (var person in workload
                     .Where(w => w.AssigneeId.HasValue && w.StoryPoints > OverloadPointThreshold)
                     .OrderByDescending(w => w.StoryPoints)
                     .Take(2))
        {
            items.Add(new AttentionItemDto(
                "Overloaded",
                "Warning",
                $"{person.AssigneeName} overloaded · {person.StoryPoints} pts",
                "sprint-planning",
                person.AssigneeId));
        }

        // Items sitting in one column past that column's aging limit. Only the single worst
        // offender is reported: a list of thirty stale cards is a backlog, not an alert.
        //
        // Several columns may map to the same state — nothing stops a board from splitting
        // "Open" into two lanes — so group rather than key, and take the most forgiving limit
        // of the group. Flagging a card against a stricter lane it may not even be in would
        // raise an alert the reader cannot act on.
        var agingByState = columns
            .Where(c => c.AgingLimitDays > 0)
            .GroupBy(c => c.MappedState)
            .ToDictionary(g => g.Key, g => g.Max(c => c.AgingLimitDays));

        var stalest = tasks
            .Where(t => t.Category != StateCategory.Done && t.StateChangedAt.HasValue)
            .Select(t => new
            {
                Task = t,
                Days = (int)(now - t.StateChangedAt!.Value).TotalDays,
                Limit = agingByState.TryGetValue(t.State, out var limit) ? limit : 0,
            })
            .Where(x => x.Limit > 0 && x.Days > x.Limit)
            .OrderByDescending(x => x.Days)
            .FirstOrDefault();

        if (stalest is not null)
        {
            items.Add(new AttentionItemDto(
                "StaleItem",
                "Warning",
                $"{SprintTask.BuildWorkItemNumber(repoCode, stalest.Task.WorkItemNumber)} stuck {stalest.Days}d",
                "boards",
                stalest.Task.Id));
        }

        // Critical first, then the order they were added, so the loudest problem leads.
        return items
            .OrderByDescending(i => i.Severity == "Critical")
            .Take(5)
            .ToList();
    }

    /// <summary>Loads the preceding sprint's headline figures so each metric can show its direction of travel.</summary>
    /// <param name="sprints">All repository sprints, newest first.</param>
    /// <param name="selected">The sprint currently being viewed, or null when the repository has none.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The previous sprint's figures, or <c>HasPrevious: false</c> when there is no earlier sprint.</returns>
    private async Task<SprintComparisonDto> ComputeComparisonAsync(
        List<Sprint>      sprints,
        Sprint?           selected,
        CancellationToken ct)
    {
        if (selected is null) return new SprintComparisonDto(false, 0, 0, 0, 0);

        // "Previous" is by start date, not by list position, so it stays correct whichever
        // sprint the reader has selected.
        var previous = sprints
            .Where(s => s.StartDate < selected.StartDate)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefault();

        if (previous is null) return new SprintComparisonDto(false, 0, 0, 0, 0);

        var previousTasks = await db.Set<SprintTask>().AsNoTracking()
            .Where(t => t.SprintId == previous.Id && !t.IsDeleted)
            .Select(t => new { t.State, t.StoryPoints, t.CreatedAt, t.ClosedAt })
            .ToListAsync(ct);

        // Done=6, Passed=7, Failed=8, Closed=9 — the terminal states, as in the velocity query.
        var done = previousTasks.Where(t => (int)t.State >= 6).ToList();
        var closed = done.Where(t => t.ClosedAt.HasValue).ToList();

        return new SprintComparisonDto(
            true,
            done.Sum(t => t.StoryPoints),
            closed.Count == 0 ? 0 : Math.Round(closed.Average(t => (t.ClosedAt!.Value - t.CreatedAt).TotalDays), 1),
            previousTasks.Count,
            done.Count);
    }

    /// <summary>Builds the sprint report card: task/point completion rates and mid-sprint scope change.</summary>
    /// <param name="sprint">The selected sprint, or null when the repository has none.</param>
    /// <param name="tasks">The sprint's non-deleted tasks.</param>
    /// <param name="storyPoints">Already-computed committed/completed story points.</param>
    /// <returns>A <see cref="SprintReportDto"/>.</returns>
    private static SprintReportDto ComputeReport(Sprint? sprint, List<SprintTask> tasks, StoryPointsDto storyPoints)
    {
        if (sprint is null || tasks.Count == 0)
            return new SprintReportDto(0, 0, 0, 0, 0, 0);

        var total     = tasks.Count;
        var completed = tasks.Count(t => t.Category == StateCategory.Done);

        // Scope added mid-sprint: tasks whose creation date falls after the sprint's start date.
        // Approximate — a task moved into the sprint later shows its original creation date, not the move.
        var sprintStart = sprint.StartDate.ToDateTime(TimeOnly.MinValue);
        var addedLate   = tasks.Where(t => t.CreatedAt > sprintStart).ToList();

        return new SprintReportDto(
            total,
            completed,
            total == 0 ? 0 : Math.Round((double)completed / total, 3),
            storyPoints.Committed == 0 ? 0 : Math.Round((double)storyPoints.Completed / storyPoints.Committed, 3),
            addedLate.Count,
            addedLate.Sum(t => t.StoryPoints));
    }

    /// <summary>Compares actual vs ideal burndown at today's working day to classify sprint health.</summary>
    /// <param name="sprint">The selected sprint.</param>
    /// <param name="burndown">Computed ideal/actual burndown arrays for the sprint.</param>
    /// <returns>Health status, working days remaining, and today's ideal/actual remaining points.</returns>
    private static SprintHealthDto ComputeSprintHealth(Sprint sprint, BurndownDataDto burndown)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysRemaining = GetWorkingDays(today > sprint.StartDate ? today : sprint.StartDate, sprint.EndDate).Count;

        if (burndown.Actual.Count == 0 || burndown.Ideal.Count == 0)
            return new SprintHealthDto("OnTrack", daysRemaining, 0, 0);

        var actualIndex = burndown.Actual.Count - 1;
        var idealIndex = Math.Min(actualIndex, burndown.Ideal.Count - 1);
        var actualRemaining = burndown.Actual[actualIndex];
        var idealRemaining = burndown.Ideal[idealIndex];

        var status = actualRemaining <= idealRemaining * 1.05
            ? "OnTrack"
            : actualRemaining <= idealRemaining * 1.25
                ? "AtRisk"
                : "Behind";

        return new SprintHealthDto(status, daysRemaining, idealRemaining, actualRemaining);
    }

    /// <summary>Computes the approximate average cycle time (CreatedAt to ClosedAt) for completed items.</summary>
    /// <param name="tasks">Tasks in the selected sprint.</param>
    /// <returns>Average days to close and the sample size it is based on.</returns>
    private static CycleTimeDto ComputeCycleTime(List<SprintTask> tasks)
    {
        var closed = tasks.Where(t => t.Category == StateCategory.Done && t.ClosedAt.HasValue).ToList();
        if (closed.Count == 0) return new CycleTimeDto(0, 0);

        var averageDays = closed.Average(t => (t.ClosedAt!.Value - t.CreatedAt).TotalDays);
        return new CycleTimeDto(Math.Round(averageDays, 1), closed.Count);
    }

    /// <summary>Selects the sprint containing today, or falls back to the most recent sprint.</summary>
    /// <param name="sprints">All repository sprints ordered by start date descending.</param>
    /// <returns>The best candidate sprint, or null when the repository has no sprints.</returns>
    private static Sprint? SelectDefaultSprint(List<Sprint> sprints)
    {
        if (sprints.Count == 0) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return sprints.FirstOrDefault(s => s.StartDate <= today && today <= s.EndDate)
               ?? sprints.First();
    }

    /// <summary>Computes ideal and actual burndown arrays, one element per sprint working day.</summary>
    /// <param name="sprint">The sprint providing the date range.</param>
    /// <param name="tasks">All tasks in the sprint.</param>
    /// <returns>A <see cref="BurndownDataDto"/> with ideal and actual point arrays.</returns>
    private static BurndownDataDto ComputeBurndown(Sprint sprint, List<SprintTask> tasks)
    {
        var workingDays = GetWorkingDays(sprint.StartDate, sprint.EndDate);
        if (workingDays.Count == 0) return new BurndownDataDto([], []);

        var totalPoints = (double)tasks.Sum(t => t.StoryPoints);
        if (totalPoints == 0)
            return new BurndownDataDto(
                Enumerable.Repeat(0d, workingDays.Count).ToList(),
                Enumerable.Repeat(0d, workingDays.Count).ToList());

        var divisor = Math.Max(1, workingDays.Count - 1);
        var ideal = workingDays
            .Select((_, i) => Math.Round(totalPoints * (1d - (double)i / divisor), 1))
            .ToList<double>();

        var today  = DateOnly.FromDateTime(DateTime.UtcNow);
        var actual = new List<double>();
        foreach (var day in workingDays)
        {
            if (day > today) break;
            var closedPoints = tasks
                .Where(t => t.ClosedAt.HasValue && DateOnly.FromDateTime(t.ClosedAt.Value) <= day)
                .Sum(t => t.StoryPoints);
            actual.Add(Math.Round(totalPoints - closedPoints, 1));
        }

        return new BurndownDataDto(ideal, actual);
    }

    /// <summary>Enumerates weekday dates within the sprint range.</summary>
    /// <param name="start">Sprint start date (inclusive).</param>
    /// <param name="end">Sprint end date (inclusive).</param>
    /// <returns>List of working days in chronological order.</returns>
    private static List<DateOnly> GetWorkingDays(DateOnly start, DateOnly end)
    {
        var days = new List<DateOnly>();
        for (var d = start; d <= end; d = d.AddDays(1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                days.Add(d);
        return days;
    }
}
