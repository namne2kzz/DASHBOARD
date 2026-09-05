using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Users.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Queries.SearchUsers;

/// <summary>Handles <see cref="SearchUsersQuery"/>: returns up to 20 active users in the caller's organization matching the search term, excluding existing repo members when requested.</summary>
public sealed class SearchUsersQueryHandler(IApplicationDbContext db, IRequestUserContext user)
    : IRequestHandler<SearchUsersQuery, IReadOnlyList<UserPickerItemDto>>
{
    /// <summary>Filters active users in the caller's org by name/email and excludes users already in the target repository. This powers the "add existing org user" (sync) flow.</summary>
    /// <param name="query">The search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Up to 20 matching <see cref="UserPickerItemDto"/> ordered by name.</returns>
    public async Task<IReadOnlyList<UserPickerItemDto>> Handle(SearchUsersQuery query, CancellationToken ct)
    {
        var term  = query.Term.Trim().ToLower();
        var orgId = user.OrgId;

        var userQuery = db.Set<User>()
            .AsNoTracking()
            .Where(u => u.OrgId == orgId && !u.IsDeleted &&
                        (u.Name.ToLower().Contains(term) || u.Email.ToLower().Contains(term)));

        if (query.ExcludeRepoId.HasValue)
        {
            var existingMemberIds = await db.Set<RepositoryMember>()
                .AsNoTracking()
                .Where(m => m.RepositoryId == query.ExcludeRepoId.Value)
                .Select(m => m.UserId)
                .ToListAsync(ct);

            userQuery = userQuery.Where(u => !existingMemberIds.Contains(u.Id));
        }

        return await userQuery
            .OrderBy(u => u.Name)
            .Take(20)
            .Select(u => new UserPickerItemDto(u.Id, u.Name, u.Email, u.AvatarClass))
            .ToListAsync(ct);
    }
}
