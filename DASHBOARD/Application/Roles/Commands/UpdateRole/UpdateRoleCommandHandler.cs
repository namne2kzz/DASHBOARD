using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Roles.Commands.UpdateRole;

/// <summary>Handles <see cref="UpdateRoleCommand"/>: checks ManageSettings permission, blocks default roles, validates name uniqueness, then persists changes.</summary>
public sealed class UpdateRoleCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateRoleCommand, Result>
{
    /// <summary>Validates permission, blocks default roles, checks name uniqueness, guards against orphaning ManageSettings, applies changes, and commits.</summary>
    /// <param name="command">The update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(UpdateRoleCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageSettings, ct))
            return Result.Failure("You do not have permission to manage roles in this repository.");

        var role = await db.Set<Role>()
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == command.RoleId && r.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(Role), command.RoleId);

        if (role.IsDefault)
            return Result.Failure("Default roles cannot be modified.");

        // Ensure renamed role doesn't conflict with another existing custom role.
        var nameConflict = await db.Set<Role>()
            .AnyAsync(r => !r.IsDefault && r.RepositoryId == command.RepositoryId && r.Name == command.Name && r.Id != command.RoleId, ct);
        if (nameConflict)
            return Result.Failure($"A role named '{command.Name}' already exists in this repository.");

        // Guard: if this role is currently the only source of ManageSettings for some members,
        // stripping it from AllowedFunctions must not leave the repository with nobody able to manage settings.
        if (role.AllowedFunctions.Contains(SystemFunction.ManageSettings) && !command.AllowedFunctions.Contains(SystemFunction.ManageSettings))
        {
            var membersOnThisRole = await db.Set<RepositoryMember>()
                .AsNoTracking()
                .Where(m => m.RepositoryId == command.RepositoryId && m.RoleId == role.Id)
                .Select(m => m.Id)
                .ToListAsync(ct);

            var stillHasManageSettingsElsewhere = await db.Set<RepositoryMember>()
                .AsNoTracking()
                .Include(m => m.Role)
                .Where(m => m.RepositoryId == command.RepositoryId && m.RoleId != role.Id)
                .ToListAsync(ct);

            if (membersOnThisRole.Count > 0 &&
                !stillHasManageSettingsElsewhere.Any(m => m.Role!.AllowedFunctions.Contains(SystemFunction.ManageSettings)))
                return Result.Failure("Cannot remove ManageSettings from this role: it is the only source of that permission in the repository.");
        }

        role.Name             = command.Name;
        role.Description      = command.Description;
        role.AllowedFunctions = command.AllowedFunctions;
        role.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
