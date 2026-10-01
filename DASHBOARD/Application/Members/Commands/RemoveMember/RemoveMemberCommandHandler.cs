using DASHBOARD.Application.Common.Caching;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Guards;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationEvents;

namespace DASHBOARD.Application.Members.Commands.RemoveMember;

/// <summary>Handles <see cref="RemoveMemberCommand"/>: keeps at least one member with <see cref="SystemFunction.ManageMembers"/> in the repository, then removes the membership row.</summary>
public sealed class RemoveMemberCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow,
    IQueryCache           cache,
    IPublishEndpoint      publisher) : IRequestHandler<RemoveMemberCommand, Result>
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

        // After the commit, never before: an eviction ahead of a failed commit would drop a valid
        // entry and let the next reader repopulate it from pre-commit state.
        await cache.RemoveByTagAsync(CacheKeys.RepositoryTag(command.RepositoryId), ct);

        // Matters most on removal: until HUB drops its cached memberships, a user who just lost access
        // still passes HUB's membership check for up to three minutes.
        await publisher.Publish(new MemberDirectoryChangedEvent(command.RepositoryId, member.UserId), ct);

        return Result.Ok;
    }
}
