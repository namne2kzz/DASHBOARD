using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.Roles.Commands.UpdateRole;

/// <summary>Updates the name, description, and allowed functions of a custom role. Default roles cannot be modified.</summary>
/// <param name="RepositoryId">The owning repository (used for permission check).</param>
/// <param name="RoleId">The role to update.</param>
/// <param name="Name">New display name.</param>
/// <param name="Description">New description.</param>
/// <param name="AllowedFunctions">Replacement permission set.</param>
public sealed record UpdateRoleCommand(
    Guid                 RepositoryId,
    Guid                 RoleId,
    string               Name,
    string               Description,
    List<SystemFunction> AllowedFunctions) : IRequest<Result>;
