namespace DASHBOARD.Application.Capacity.DTOs;

/// <summary>Capacity configuration for one team member within a sprint.</summary>
public sealed record CapacityMemberDto(
    Guid     Id,
    Guid     SprintId,
    Guid     UserId,
    string   UserName,
    string   UserAvatar,
    string   Role,
    decimal  HoursPerDay,
    decimal  OvertimeHoursPerDay);

/// <summary>A day-off entry deducting hours from a member's sprint capacity.</summary>
public sealed record DayOffDto(
    Guid     Id,
    Guid     SprintId,
    Guid?    UserId,
    string?  UserName,
    DateOnly Date,
    decimal  Hours,
    string   Reason);
