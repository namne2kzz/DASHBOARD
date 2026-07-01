using DASHBOARD.Application.Capacity.DTOs;
using DASHBOARD.Application.SprintTasks.DTOs;

namespace DASHBOARD.Application.Sprints.DTOs;

/// <summary>Per-member capacity vs workload breakdown for sprint planning.</summary>
public sealed record MemberLoadDto(
    Guid    UserId,
    string  UserName,
    string  UserAvatar,
    decimal HoursPerDay,
    decimal OvertimeHoursPerDay,
    decimal PersonalDaysOffHours,
    decimal Capacity,
    decimal Workload,
    int     LoadPercent,
    string  LoadState);

/// <summary>Full sprint planning snapshot: sprint metadata, tasks, capacity configuration, and computed member loads.</summary>
public sealed record SprintDetailDto(
    Guid     Id,
    Guid     RepositoryId,
    string   Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool     IsActive,
    int      WorkingDays,
    IReadOnlyList<SprintTaskDto>     Tasks,
    IReadOnlyList<CapacityMemberDto> CapacityMembers,
    IReadOnlyList<DayOffDto>         DaysOff,
    IReadOnlyList<MemberLoadDto>     MemberLoads);
