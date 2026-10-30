using DASHBOARD.Application.Common.Caching;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Guards;
using DASHBOARD.Application.Common.Helpers;
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
    IPublishEndpoint      publisher,
    INotificationService  notifications) : IRequestHandler<RemoveMemberCommand, Result>
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

        // Guard: the repository must always retain somebody who can lead it — judged by actual
        // permission, not by what their role is called (roles.dod.md §3).
        if (await RepositoryLeadGuard.WouldOrphanLeadAsync(db, command.RepositoryId, command.MemberId, overrideRoleId: null, ct))
            return Result.Failure("Cannot remove the last member who can manage members. Assign that permission to another member first.");

        var removedUserId = member.UserId;

        db.Set<RepositoryMember>().Remove(member);
        await uow.CommitAsync(ct);

        // After the commit, never before: an eviction ahead of a failed commit would drop a valid
        // entry and let the next reader repopulate it from pre-commit state.
        await cache.RemoveByTagAsync(CacheKeys.RepositoryTag(command.RepositoryId), ct);

        // Matters most on removal: until HUB drops its cached memberships, a user who just lost access
        // still passes HUB's membership check for up to three minutes.
        await publisher.Publish(new MemberDirectoryChangedEvent(command.RepositoryId, removedUserId), ct);

        var repoName = await db.Set<Repository>().AsNoTracking()
            .Where(r => r.Id == command.RepositoryId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync(ct);

        if (await NotifyPrefsHelper.PushEnabledForTypeAsync(db, removedUserId, NotificationType.RemovedFromRepository, ct))
            await notifications.NotifyAsync(
                removedUserId,
                NotificationType.RemovedFromRepository,
                $"Removed from \"{repoName}\"",
                "You have been removed from this repository.",
                url: null, ct);

        return Result.Ok;
    }
}
