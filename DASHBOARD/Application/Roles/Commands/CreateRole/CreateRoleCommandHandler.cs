using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Roles.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Roles.Commands.CreateRole;

/// <summary>Handles <see cref="CreateRoleCommand"/>: checks ManageRoles permission, validates name uniqueness within the repo, then persists the custom role.</summary>
public sealed class CreateRoleCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<CreateRoleCommand, Result<RoleDto>>
{
    /// <summary>Validates permissions and name uniqueness, then creates and returns the new role.</summary>
    /// <param name="command">The create request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result{T}.Success"/> with the newly created <see cref="RoleDto"/>; <see cref="Result{T}.Failure"/> on a business-rule violation.</returns>
    public async Task<Result<RoleDto>> Handle(CreateRoleCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageRoles, ct))
            throw new ForbiddenException("You do not have permission to manage roles in this repository.");

        var nameExists = await db.Set<Role>()
            .AnyAsync(r => !r.IsDefault && r.RepositoryId == command.RepositoryId && r.Name == command.Name, ct);
        if (nameExists)
            return Result<RoleDto>.Failure($"A role named '{command.Name}' already exists in this repository.");

        var role = new Role
        {
            RepositoryId     = command.RepositoryId,
            IsDefault        = false,
            Name             = command.Name,
            Description      = command.Description,
            AllowedFunctions = command.AllowedFunctions,
        };
        db.Set<Role>().Add(role);
        await uow.CommitAsync(ct);

        return Result<RoleDto>.Success(new RoleDto(role.Id, role.RepositoryId, role.IsDefault, role.Name, role.Description, role.AllowedFunctions, 0));
    }
}
