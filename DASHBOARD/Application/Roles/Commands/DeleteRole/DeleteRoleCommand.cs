using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Roles.Commands.DeleteRole;

/// <summary>Deletes a custom role. Fails for default roles or when any repository member is still assigned to this role.</summary>
/// <param name="RepositoryId">The owning repository (used for permission check).</param>
/// <param name="RoleId">The role to delete.</param>
public sealed record DeleteRoleCommand(Guid RepositoryId, Guid RoleId) : IRequest<Result>;
