using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Invitations.Commands.RevokeInvitation;

/// <summary>Handles <see cref="RevokeInvitationCommand"/>: checks permission and flips a Pending invitation to Revoked.</summary>
public sealed class RevokeInvitationCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<RevokeInvitationCommand, Result>
{
    /// <summary>Validates permission, ensures the invitation is still Pending, then marks it Revoked.</summary>
    /// <param name="command">The revoke command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> when the invitation is not Pending.</returns>
    public async Task<Result> Handle(RevokeInvitationCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.InviteMembers, ct))
            throw new ForbiddenException("You do not have permission to manage invitations in this repository.");

        var invitation = await db.Set<Invitation>().AsTracking()
            .FirstOrDefaultAsync(i => i.Id == command.InvitationId && i.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(Invitation), command.InvitationId);

        if (invitation.Status != InvitationStatus.Pending)
            return Result.Failure("Only pending invitations can be revoked.");

        invitation.Status = InvitationStatus.Revoked;
        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
