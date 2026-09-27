using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Sprints.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Sprints.Queries.GetSprintSummary;

/// <summary>Handles <see cref="GetSprintSummaryQuery"/>: aggregates capacity, committed points, and task completion into a summary DTO.</summary>
public sealed class GetSprintSummaryQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetSprintSummaryQuery, SprintSummaryDto>
{
    /// <summary>Loads the sprint, aggregates capacity rows and task states, and returns the summary.</summary>
    /// <param name="query">The summary query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SprintSummaryDto"/> with velocity and capacity data.</returns>
    public async Task<SprintSummaryDto> Handle(GetSprintSummaryQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var sprint = await db.Set<Sprint>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.SprintId && s.RepositoryId == query.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(Sprint), query.SprintId);

        // Working days = calendar days minus weekends.
        int workingDays = 0;
        for (var d = sprint.StartDate; d <= sprint.EndDate; d = d.AddDays(1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                workingDays++;

        // Total capacity: sum of (HoursPerDay + OvertimeHoursPerDay) × working days per member, minus days off.
        var capacityRows = await db.Set<CapacityMember>().AsNoTracking()
            .Where(c => c.SprintId == query.SprintId)
            .ToListAsync(ct);

        var daysOff = await db.Set<DayOff>().AsNoTracking()
            .Where(d => d.SprintId == query.SprintId)
            .ToListAsync(ct);

        decimal totalCapacity = 0m;
        foreach (var cap in capacityRows)
        {
            var dailyHours = cap.HoursPerDay + cap.OvertimeHoursPerDay;

            var personalOffHours = daysOff
                .Where(d => d.UserId == cap.UserId)
                .Sum(d => d.Hours);

            // A team-wide day off cannot remove more than the member actually works that day:
            // a company holiday of 8 h costs a 4 h/day member only their own 4 h.
            var teamOffHours = daysOff
                .Where(d => d.UserId == null)
                .Sum(d => Math.Min(d.Hours, dailyHours));

            // Clamped per member, so one heavily absent person cannot eat into someone else's capacity.
            totalCapacity += Math.Max(0, dailyHours * workingDays - personalOffHours - teamOffHours);
        }

        // Sprint tasks: story points and completion.
        var tasks = await db.Set<SprintTask>().AsNoTracking()
            .Where(t => t.SprintId == query.SprintId)
            .ToListAsync(ct);

        var committedPoints  = tasks.Sum(t => t.StoryPoints);
        var completedPoints  = tasks.Where(t => t.State == SprintTaskState.Done).Sum(t => t.StoryPoints);
        var completedTasks   = tasks.Count(t => t.State == SprintTaskState.Done);

        return new SprintSummaryDto(
            sprint.Id, sprint.Name,
            workingDays,
            totalCapacity,
            committedPoints,
            completedPoints,
            tasks.Count,
            completedTasks);
    }
}
