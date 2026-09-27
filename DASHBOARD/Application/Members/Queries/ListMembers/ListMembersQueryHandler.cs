using DASHBOARD.Application.Common.Caching;
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
    IRequestUserContext   user,
    IQueryCache           cache) : IRequestHandler<ListMembersQuery, IReadOnlyList<MemberDto>>
{
    /// <summary>Validates membership, then serves the member list from cache or loads it.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All visible <see cref="MemberDto"/> records ordered by user name.</returns>
    public async Task<IReadOnlyList<MemberDto>> Handle(ListMembersQuery query, CancellationToken ct)
    {
        // Runs on every request, never cached: the cached payload is keyed by repository, so the
        // caller's right to see it must be proven fresh each time rather than inherited from
        // whoever happened to warm the entry.
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        return await cache.GetOrCreateAsync(
            CacheKeys.Members(query.RepositoryId, query.RoleFilter),
            token => LoadAsync(query, token),
            tags: [CacheKeys.RepositoryTag(query.RepositoryId)],
            ct: ct);
    }

    /// <summary>Loads member rows from the database and projects them to DTOs.</summary>
    /// <param name="query">The list query carrying the repository and optional role filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Member DTOs ordered by user name.</returns>
    private async Task<IReadOnlyList<MemberDto>> LoadAsync(ListMembersQuery query, CancellationToken ct)
    {
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
