using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Roles.Commands.DeleteRole;

/// <summary>Handles <see cref="DeleteRoleCommand"/>: checks ManageSettings permission, blocks default roles, blocks deletion when members are still assigned, then removes the role.</summary>
public sealed class DeleteRoleCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<DeleteRoleCommand, Result>
{
    /// <summary>Validates permission, blocks default roles, checks for assigned members, and deletes the role.</summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> when the role is default or members are still assigned.</returns>
    public async Task<Result> Handle(DeleteRoleCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageSettings, ct))
            return Result.Failure("You do not have permission to manage roles in this repository.");

        var role = await db.Set<Role>()
            .FirstOrDefaultAsync(r => r.Id == command.RoleId && r.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(Role), command.RoleId);

        if (role.IsDefault)
            return Result.Failure("Default roles cannot be deleted.");

        var assignedCount = await db.Set<RepositoryMember>()
            .CountAsync(m => m.RoleId == command.RoleId, ct);

        if (assignedCount > 0)
            return Result.Failure($"Cannot delete role '{role.Name}': {assignedCount} member(s) are still assigned to it. Reassign them first.");

        db.Set<Role>().Remove(role);
        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
