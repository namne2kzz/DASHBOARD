using DASHBOARD.Application.Capacity.DTOs;
using MediatR;

namespace DASHBOARD.Application.Capacity.Commands.UpsertCapacityMember;

/// <summary>Creates or updates a team member's capacity row for a sprint. Idempotent — updates hours if the row already exists.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint to configure capacity for.</param>
/// <param name="UserId">The team member.</param>
/// <param name="Role">The member's role for this sprint.</param>
/// <param name="HoursPerDay">Regular daily hours (0–12).</param>
/// <param name="OvertimeHoursPerDay">Overtime daily hours (0–6).</param>
public sealed record UpsertCapacityMemberCommand(
    Guid     RepositoryId,
    Guid     SprintId,
    Guid     UserId,
    string   Role,
    decimal  HoursPerDay,
    decimal  OvertimeHoursPerDay) : IRequest<CapacityMemberDto>;
