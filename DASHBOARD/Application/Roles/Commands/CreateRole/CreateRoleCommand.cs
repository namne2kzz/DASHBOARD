using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Roles.DTOs;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.Roles.Commands.CreateRole;

/// <summary>Creates a new custom role within a repository. Requires <see cref="SystemFunction.ManageSettings"/>.</summary>
/// <param name="RepositoryId">The repository to add the role to.</param>
/// <param name="Name">Display name of the role.</param>
/// <param name="Description">Purpose description.</param>
/// <param name="AllowedFunctions">List of permitted system functions.</param>
public sealed record CreateRoleCommand(
    Guid                    RepositoryId,
    string                  Name,
    string                  Description,
    List<SystemFunction>    AllowedFunctions) : IRequest<Result<RoleDto>>;
