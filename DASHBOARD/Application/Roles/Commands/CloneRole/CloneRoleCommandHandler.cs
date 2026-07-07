using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Roles.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Roles.Commands.CloneRole;

/// <summary>Handles <see cref="CloneRoleCommand"/>: copies the source role's permissions into a new custom role under a new name.</summary>
public sealed class CloneRoleCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<CloneRoleCommand, Result<RoleDto>>
{
    /// <summary>Validates permission, checks name uniqueness, clones the role as a custom role, and returns the new DTO.</summary>
    /// <param name="command">The clone command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result{T}.Success"/> with the cloned <see cref="RoleDto"/>; <see cref="Result{T}.Failure"/> on a business-rule violation.</returns>
    public async Task<Result<RoleDto>> Handle(CloneRoleCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageRoles, ct))
            return Result<RoleDto>.Failure("You do not have permission to manage roles in this repository.");

        // Source may be a global default role or a custom role within this repository.
        var source = await db.Set<Role>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == command.SourceRoleId && (r.IsDefault || r.RepositoryId == command.RepositoryId), ct);
        if (source is null)
            return Result<RoleDto>.Failure("Role not found in this repository.");

        if (await db.Set<Role>().AnyAsync(r => !r.IsDefault && r.RepositoryId == command.RepositoryId && r.Name == command.NewName, ct))
            return Result<RoleDto>.Failure($"A role named '{command.NewName}' already exists in this repository.");

        var clone = new Role
        {
            RepositoryId     = command.RepositoryId,
            IsDefault        = false,
            Name             = command.NewName,
            Description      = $"Clone of '{source.Name}'",
            AllowedFunctions = [..source.AllowedFunctions],
        };
        db.Set<Role>().Add(clone);
        await uow.CommitAsync(ct);

        return Result<RoleDto>.Success(new RoleDto(clone.Id, clone.RepositoryId, clone.IsDefault, clone.Name, clone.Description, clone.AllowedFunctions, 0));
    }
}
