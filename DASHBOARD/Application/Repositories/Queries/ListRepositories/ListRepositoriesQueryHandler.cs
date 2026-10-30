using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Repositories.DTOs;
using DASHBOARD.Domain.Constants;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Repositories.Queries.ListRepositories;

/// <summary>Handles <see cref="ListRepositoriesQuery"/>: returns repositories visible to the caller with member counts.</summary>
public sealed class ListRepositoriesQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListRepositoriesQuery, IReadOnlyList<RepositoryDto>>
{
    /// <summary>Filters repositories by membership (or all for global admins), applies search, and projects to DTOs.</summary>
    /// <param name="query">The list query with optional filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of visible <see cref="RepositoryDto"/> items ordered by name.</returns>
    public async Task<IReadOnlyList<RepositoryDto>> Handle(ListRepositoriesQuery query, CancellationToken ct)
    {
        var isAdmin = await user.IsGlobalAdminAsync(ct);
        var orgId   = user.OrgId;

        // Multi-tenant: everything is scoped to the caller's organization.
        var repoQuery = db.Set<Repository>().AsNoTracking().Where(r => r.OrgId == orgId);

        // Org admins see every repo in their org; everyone else only the ones they belong to.
        if (!isAdmin)
        {
            var memberRepoIds = await db.Set<RepositoryMember>()
                .AsNoTracking()
                .Where(m => m.UserId == user.UserId)
                .Select(m => m.RepositoryId)
                .ToListAsync(ct);

            repoQuery = repoQuery.Where(r => memberRepoIds.Contains(r.Id));
        }

        // Archived repositories stay out of the way unless an admin asks for them, which the
        // administration screen does so they can be reviewed and restored. Asking as a non-admin
        // changes nothing: the flag is ignored rather than refused, since an ordinary member has
        // no archived repositories of their own to reveal.
        if (!query.IncludeArchived || !isAdmin)
            repoQuery = repoQuery.Where(r => !r.IsArchived);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            repoQuery = repoQuery.Where(r => r.Name.ToLower().Contains(term) || r.Code.ToLower().Contains(term));
        }

        var repos = await repoQuery.OrderBy(r => r.Name).ToListAsync(ct);

        var repoIds = repos.Select(r => r.Id).ToList();
        var memberCounts = await db.Set<RepositoryMember>()
            .AsNoTracking()
            .Where(m => repoIds.Contains(m.RepositoryId))
            .GroupBy(m => m.RepositoryId)
            .Select(g => new { RepositoryId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var countMap = memberCounts.ToDictionary(x => x.RepositoryId, x => x.Count);

        // The lead is a membership carrying the Project Manager role, not a column on the repository.
        // A repository can briefly have none — if the holder was removed — so this is a left join
        // in spirit: absence yields nulls rather than dropping the repository from the list.
        var leads = await db.Set<RepositoryMember>()
            .AsNoTracking()
            .Where(m => repoIds.Contains(m.RepositoryId)
                        && m.DefaultRole == DefaultRoleDefinitions.ProjectManager)
            .Join(db.Set<User>().AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new { m.RepositoryId, u.Id, u.Name })
            .ToListAsync(ct);

        var leadMap = leads
            .GroupBy(x => x.RepositoryId)
            .ToDictionary(g => g.Key, g => g.First());

        return repos.Select(r =>
        {
            var lead = leadMap.GetValueOrDefault(r.Id);
            return new RepositoryDto(
                r.Id, r.Name, r.Code, r.Description,
                countMap.GetValueOrDefault(r.Id, 0),
                r.CreatedAt,
                r.IsArchived,
                lead?.Id,
                lead?.Name);
        }).ToList();
    }
}
