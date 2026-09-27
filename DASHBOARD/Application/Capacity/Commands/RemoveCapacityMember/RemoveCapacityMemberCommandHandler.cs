using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Capacity.Commands.RemoveCapacityMember;

/// <summary>
/// Handles <see cref="RemoveCapacityMemberCommand"/>: checks ManageCapacity permission then removes the row,
/// and fire-and-forget syncs the removal to the sprint's linked HUB channel (if one exists).
/// </summary>
public sealed class RemoveCapacityMemberCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow,
    IHubChannelService    hub) : IRequestHandler<RemoveCapacityMemberCommand, Result>
{
    /// <summary>Validates permission, removes the capacity row, and removes the member from the HUB channel.</summary>
    /// <param name="command">The remove command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(RemoveCapacityMemberCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageCapacity, ct))
            throw new ForbiddenException("You do not have permission to manage capacity in this repository.");

        var cap = await db.Set<CapacityMember>()
            .FirstOrDefaultAsync(c => c.Id == command.CapacityMemberId && c.SprintId == command.SprintId, ct)
            ?? throw new NotFoundException(nameof(CapacityMember), command.CapacityMemberId);

        var removedUserId = cap.UserId;

        // Look up channel link before removing (DbContext is in scope)
        var link = await db.Set<SprintChannelLink>()
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.SprintId == command.SprintId, ct);

        db.Set<CapacityMember>().Remove(cap);
        await uow.CommitAsync(ct);

        // ── Fire-and-forget: remove member from HUB channel ────────────────────
        // The removal is already committed, so a HUB client fault must not reach the caller.
        // Discarding the task only covers a faulted task — a client that throws before returning
        // one would otherwise escape the handler.
        if (link is not null)
        {
            try
            {
                _ = hub.RemoveMemberAsync(link.HubChannelId, removedUserId, CancellationToken.None);
            }
            catch
            {
                // Best-effort sync — the capacity row is removed either way.
            }
        }

        return Result.Ok;
    }
}
