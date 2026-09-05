using DASHBOARD.Application.Capacity.DTOs;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Capacity.Commands.UpsertCapacityMember;

/// <summary>
/// Handles <see cref="UpsertCapacityMemberCommand"/>: validates membership, creates or updates the capacity row,
/// and fire-and-forget syncs the member to the sprint's linked HUB channel (if one exists).
/// </summary>
public sealed class UpsertCapacityMemberCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow,
    IHubChannelService    hub) : IRequestHandler<UpsertCapacityMemberCommand, CapacityMemberDto>
{
    /// <summary>Validates ManageCapacity permission, upserts the capacity row, and returns the DTO.</summary>
    /// <param name="command">The upsert command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created or updated <see cref="CapacityMemberDto"/>.</returns>
    public async Task<CapacityMemberDto> Handle(UpsertCapacityMemberCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageCapacity, ct))
            throw new UnauthorizedAccessException("You do not have permission to manage capacity in this repository.");

        if (!await db.Set<Sprint>().AnyAsync(s => s.Id == command.SprintId && s.RepositoryId == command.RepositoryId, ct))
            throw new NotFoundException(nameof(Sprint), command.SprintId);

        if (!await user.IsMemberOfAsync(command.RepositoryId, ct))
            throw new InvalidOperationException("The specified user is not a member of this repository.");

        var memberUser = await db.Set<User>().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == command.UserId, ct)
            ?? throw new NotFoundException(nameof(User), command.UserId);

        var existing = await db.Set<CapacityMember>().AsTracking()
            .FirstOrDefaultAsync(c => c.SprintId == command.SprintId && c.UserId == command.UserId, ct);

        if (existing is null)
        {
            existing = new CapacityMember
            {
                SprintId     = command.SprintId,
                RepositoryId = command.RepositoryId,
                UserId       = command.UserId,
                Role         = command.Role,
                HoursPerDay  = command.HoursPerDay,
                OvertimeHoursPerDay = command.OvertimeHoursPerDay,
            };
            db.Set<CapacityMember>().Add(existing);
        }
        else
        {
            existing.Role                = command.Role;
            existing.HoursPerDay         = command.HoursPerDay;
            existing.OvertimeHoursPerDay = command.OvertimeHoursPerDay;
            existing.Touch();
        }

        await uow.CommitAsync(ct);

        // ── Fire-and-forget: sync new member to HUB channel ────────────────────
        // Look up the channel link before leaving the handler (DbContext is in scope here).
        var link = await db.Set<SprintChannelLink>()
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.SprintId == command.SprintId, ct);

        if (link is not null)
            _ = hub.AddMemberAsync(link.HubChannelId, command.UserId, CancellationToken.None);

        return new CapacityMemberDto(existing.Id, existing.SprintId, existing.UserId,
            memberUser.Name, memberUser.AvatarClass, existing.Role, existing.HoursPerDay, existing.OvertimeHoursPerDay);
    }
}
