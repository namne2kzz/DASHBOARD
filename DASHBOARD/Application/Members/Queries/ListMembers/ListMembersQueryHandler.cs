using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Members.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Members.Queries.ListMembers;

/// <summary>Handles <see cref="ListMembersQuery"/>: checks membership, loads member rows with user and role details.</summary>
public sealed class ListMembersQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListMembersQuery, IReadOnlyList<MemberDto>>
{
    /// <summary>Validates membership, applies optional role filter, and projects member rows to DTOs.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All visible <see cref="MemberDto"/> records ordered by user name.</returns>
    public async Task<IReadOnlyList<MemberDto>> Handle(ListMembersQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var q = db.Set<RepositoryMember>()
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.Role)
            .Where(m => m.RepositoryId == query.RepositoryId);

        if (!string.IsNullOrEmpty(query.RoleFilter))
            q = q.Where(m => m.DefaultRole == query.RoleFilter);

        var members = await q.OrderBy(m => m.User!.Name).ToListAsync(ct);

        return members.Select(m => new MemberDto(
            m.Id,
            m.UserId,
            m.User!.Name,
            m.User.Email,
            m.User.AvatarClass,
            m.DefaultRole,
            m.RoleId,
            m.Role?.Name,
            m.CreatedAt)).ToList();
    }
}
