using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Roles.DTOs;
using MediatR;

namespace DASHBOARD.Application.Roles.Commands.CloneRole;

/// <summary>Creates a custom copy of an existing role (default or custom) with a new name. Useful for bootstrapping similar roles.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SourceRoleId">The role to clone.</param>
/// <param name="NewName">Name for the cloned role.</param>
public sealed record CloneRoleCommand(
    Guid   RepositoryId,
    Guid   SourceRoleId,
    string NewName) : IRequest<Result<RoleDto>>;
