using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Repositories.DTOs;
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

        // Org admins see all (non-archived) repos in their org; everyone else only repos they belong to.
        if (isAdmin)
        {
            repoQuery = repoQuery.Where(r => !r.IsArchived);
        }
        else
        {
            var memberRepoIds = await db.Set<RepositoryMember>()
                .AsNoTracking()
                .Where(m => m.UserId == user.UserId)
                .Select(m => m.RepositoryId)
                .ToListAsync(ct);

            repoQuery = repoQuery.Where(r => memberRepoIds.Contains(r.Id) && !r.IsArchived);
        }

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

        return repos.Select(r => new RepositoryDto(
            r.Id, r.Name, r.Code, r.Description,
            countMap.GetValueOrDefault(r.Id, 0),
            r.CreatedAt)).ToList();
    }
}
