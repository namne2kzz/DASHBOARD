using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Guards;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Members.Commands.UpdateMemberRole;

/// <summary>Handles <see cref="UpdateMemberRoleCommand"/>: keeps at least one member with <see cref="SystemFunction.ManageMembers"/> in the repository, then updates the role assignment.</summary>
public sealed class UpdateMemberRoleCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateMemberRoleCommand, Result>
{
    /// <summary>Validates permission and the ManageMembers guard, then applies the role change.</summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> when business rules prevent the change.</returns>
    public async Task<Result> Handle(UpdateMemberRoleCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageMembers, ct))
            throw new ForbiddenException("You do not have permission to manage members in this repository.");

        var member = await db.Set<RepositoryMember>()
            .AsTracking()
            .FirstOrDefaultAsync(m => m.Id == command.MemberId && m.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(RepositoryMember), command.MemberId);

        // Validate new Role is either a global default role or a custom role within this repo.
        var newRole = await db.Set<Role>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == command.RoleId && (r.IsDefault || r.RepositoryId == command.RepositoryId), ct);
        if (newRole is null)
            return Result.Failure("Role not found in this repository.");

        // Guard: the repository must always retain at least one member who can manage members (by actual permission, not discipline/role name).
        if (await ManageSettingsGuard.WouldOrphanManageSettingsAsync(db, command.RepositoryId, command.MemberId, command.RoleId, ct))
            return Result.Failure("Cannot change the last member with ManageMembers permission. Assign that permission to another member first.");

        member.DefaultRole = command.DefaultRole;
        member.RoleId      = command.RoleId;
        member.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
