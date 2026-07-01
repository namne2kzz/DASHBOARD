using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.Members.Commands.UpdateMemberRole;

/// <summary>Changes a member's default team role and optional assigned role.</summary>
/// <param name="RepositoryId">The repository the member belongs to.</param>
/// <param name="MemberId">The membership row to update.</param>
/// <param name="DefaultRole">New team role (discipline).</param>
/// <param name="RoleId">New role ID (default or custom) granting permissions. Required.</param>
public sealed record UpdateMemberRoleCommand(
    Guid     RepositoryId,
    Guid     MemberId,
    string   DefaultRole,
    Guid     RoleId) : IRequest<Result>;
