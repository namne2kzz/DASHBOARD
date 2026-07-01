using DASHBOARD.Application.Capacity.DTOs;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Sprints.DTOs;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Application.SprintTasks.Queries.ListSprintTasks;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Sprints.Queries.GetSprintDetail;

/// <summary>Handles <see cref="GetSprintDetailQuery"/>: loads sprint, tasks, capacity, and computes member loads server-side.</summary>
public sealed class GetSprintDetailQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetSprintDetailQuery, SprintDetailDto>
{
    /// <summary>Validates membership, loads all sprint data, computes member loads, and returns the consolidated DTO.</summary>
    /// <param name="query">The detail query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A fully populated <see cref="SprintDetailDto"/>.</returns>
    public async Task<SprintDetailDto> Handle(GetSprintDetailQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        var sprint = await db.Set<Sprint>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.SprintId && s.RepositoryId == query.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(Sprint), query.SprintId);

        var repoCode = await db.Set<Repository>()
            .Where(r => r.Id == query.RepositoryId).Select(r => r.Code).FirstAsync(ct);

        var tasks   = await LoadTasksAsync(query, repoCode, ct);
        var members = await LoadMembersAsync(query, ct);
        var daysOff = await LoadDaysOffAsync(query, ct);

        var workingDays  = CountWorkingDays(sprint.StartDate, sprint.EndDate);
        var memberLoads  = ComputeMemberLoads(members, daysOff, tasks, workingDays);

        var today    = DateOnly.FromDateTime(DateTime.UtcNow);
        var isActive = sprint.StartDate <= today && sprint.EndDate >= today;

        return new SprintDetailDto(
            sprint.Id, sprint.RepositoryId, sprint.Name,
            sprint.StartDate, sprint.EndDate, isActive,
            workingDays, tasks, members, daysOff, memberLoads);
    }

    private async Task<IReadOnlyList<SprintTaskDto>> LoadTasksAsync(GetSprintDetailQuery query, string repoCode, CancellationToken ct)
    {
        var all = await db.Set<SprintTask>()
            .AsNoTracking()
            .Include(t => t.AssignedTo)
            .Where(t => t.SprintId == query.SprintId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        return ListSprintTasksQueryHandler.BuildTree(all, null, repoCode);
    }

    private async Task<IReadOnlyList<CapacityMemberDto>> LoadMembersAsync(GetSprintDetailQuery query, CancellationToken ct)
        => await db.Set<CapacityMember>()
            .AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.SprintId == query.SprintId)
            .OrderBy(c => c.User!.Name)
            .Select(c => new CapacityMemberDto(
                c.Id, c.SprintId, c.UserId, c.User!.Name, c.User.AvatarClass,
                c.Role, c.HoursPerDay, c.OvertimeHoursPerDay))
            .ToListAsync(ct);

    private async Task<IReadOnlyList<DayOffDto>> LoadDaysOffAsync(GetSprintDetailQuery query, CancellationToken ct)
        => await db.Set<DayOff>()
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.SprintId == query.SprintId)
            .OrderBy(d => d.Date)
            .Select(d => new DayOffDto(
                d.Id, d.SprintId, d.UserId,
                d.User != null ? d.User.Name : null,
                d.Date, d.Hours, d.Reason))
            .ToListAsync(ct);

    private static int CountWorkingDays(DateOnly start, DateOnly end)
    {
        int count = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
        return count;
    }

    private static IReadOnlyList<MemberLoadDto> ComputeMemberLoads(
        IReadOnlyList<CapacityMemberDto> members,
        IReadOnlyList<DayOffDto>         daysOff,
        IReadOnlyList<SprintTaskDto>     tasks,
        int                              workingDays)
    {
        // Flatten sub-tasks for workload calculation (only leaf tasks carry time estimates).
        var allTasks = Flatten(tasks);

        return members.Select(m =>
        {
            var personalOffHours = daysOff
                .Where(d => d.UserId == m.UserId)
                .Sum(d => d.Hours);

            var teamOffHours = daysOff
                .Where(d => d.UserId == null)
                .Sum(d => Math.Min(d.Hours, m.HoursPerDay + m.OvertimeHoursPerDay));

            var gross    = workingDays * (m.HoursPerDay + m.OvertimeHoursPerDay);
            var capacity = Math.Max(0, gross - personalOffHours - teamOffHours);

            var workload = allTasks
                .Where(t => t.AssignedToId == m.UserId && t.State != SprintTaskState.Done)
                .Sum(t => t.RemainingWork);

            var loadPercent = capacity == 0
                ? (workload > 0 ? 999 : 0)
                : (int)Math.Round(workload / capacity * 100);

            var loadState = loadPercent > 120 ? "overloaded"
                          : loadPercent > 100 ? "warning"
                          : "safe";

            return new MemberLoadDto(
                m.UserId, m.UserName, m.UserAvatar,
                m.HoursPerDay, m.OvertimeHoursPerDay,
                personalOffHours, capacity, workload, loadPercent, loadState);
        }).ToList();
    }

    private static IEnumerable<SprintTaskDto> Flatten(IReadOnlyList<SprintTaskDto> tasks)
    {
        foreach (var t in tasks)
        {
            yield return t;
            foreach (var sub in Flatten(t.SubTasks))
                yield return sub;
        }
    }
}
