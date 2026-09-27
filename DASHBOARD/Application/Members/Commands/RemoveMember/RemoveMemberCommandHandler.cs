using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Guards;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Members.Commands.RemoveMember;

/// <summary>Handles <see cref="RemoveMemberCommand"/>: keeps at least one member with <see cref="SystemFunction.ManageMembers"/> in the repository, then removes the membership row.</summary>
public sealed class RemoveMemberCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<RemoveMemberCommand, Result>
{
    /// <summary>Checks permission and the ManageMembers guard, then deletes the membership.</summary>
    /// <param name="command">The remove command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> when the last member with ManageMembers would be removed.</returns>
    public async Task<Result> Handle(RemoveMemberCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageMembers, ct))
            throw new ForbiddenException("You do not have permission to manage members in this repository.");

        var member = await db.Set<RepositoryMember>()
            .FirstOrDefaultAsync(m => m.Id == command.MemberId && m.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(RepositoryMember), command.MemberId);

        // Guard: the repository must always retain at least one member who can manage members (by actual permission, not discipline/role name).
        if (await ManageSettingsGuard.WouldOrphanManageSettingsAsync(db, command.RepositoryId, command.MemberId, overrideRoleId: null, ct))
            return Result.Failure("Cannot remove the last member with ManageMembers permission from a repository.");

        db.Set<RepositoryMember>().Remove(member);
        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
