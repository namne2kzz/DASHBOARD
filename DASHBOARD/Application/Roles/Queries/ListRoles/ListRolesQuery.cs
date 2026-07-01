using DASHBOARD.Application.Roles.DTOs;
using MediatR;

namespace DASHBOARD.Application.Roles.Queries.ListRoles;

/// <summary>Returns all roles available to a repository — global default roles plus the repository's custom roles.</summary>
/// <param name="RepositoryId">The repository to query.</param>
public sealed record ListRolesQuery(Guid RepositoryId) : IRequest<IReadOnlyList<RoleDto>>;
