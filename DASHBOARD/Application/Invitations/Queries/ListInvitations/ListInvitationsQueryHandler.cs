using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Invitations.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Invitations.Queries.ListInvitations;

/// <summary>Handles <see cref="ListInvitationsQuery"/>: checks the InviteMembers permission, then loads every invitation for the repository.</summary>
internal sealed class ListInvitationsQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListInvitationsQuery, IReadOnlyList<InvitationListItemDto>>
{
    /// <summary>Validates permission and projects invitation rows to DTOs, newest first.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every <see cref="InvitationListItemDto"/> for the repository, ordered by send date descending.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user lacks the InviteMembers permission in this repository.</exception>
    public async Task<IReadOnlyList<InvitationListItemDto>> Handle(ListInvitationsQuery query, CancellationToken ct)
    {
        if (!await user.CanAsync(query.RepositoryId, SystemFunction.InviteMembers, ct))
            throw new ForbiddenException("You do not have permission to view invitations for this repository.");

        var invitations = await db.Set<Invitation>()
            .AsNoTracking()
            .Include(i => i.InvitedBy)
            .Include(i => i.Role)
            .Where(i => i.RepositoryId == query.RepositoryId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return invitations.Select(i => new InvitationListItemDto(
            i.Id,
            i.Email,
            i.Status,
            i.DefaultRole,
            i.Role?.Name,
            i.InvitedBy?.Name ?? "Unknown",
            i.CreatedAt,
            i.ExpiresAt,
            i.AcceptedAt)).ToList();
    }
}
