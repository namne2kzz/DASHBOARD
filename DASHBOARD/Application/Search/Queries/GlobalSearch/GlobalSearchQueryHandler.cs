using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Search.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Search.Queries.GlobalSearch;

/// <summary>
/// Handles <see cref="GlobalSearchQuery"/>: runs a case-insensitive keyword match across Sprint tasks
/// and Backlog items in the repository, returning a capped, unified result list.
/// </summary>
public sealed class GlobalSearchQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GlobalSearchQuery, IReadOnlyList<SearchResultItemDto>>
{
    private const int PerKindLimit = 6;

    /// <summary>Validates membership, then searches each entity type and merges the hits.</summary>
    /// <param name="query">The search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Up to <see cref="PerKindLimit"/> hits per entity kind, tasks first.</returns>
    public async Task<IReadOnlyList<SearchResultItemDto>> Handle(GlobalSearchQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        var term = query.Term.Trim().ToLower();
        if (term.Length < 2)
            return [];

        var repoCode = await db.Set<Repository>().AsNoTracking()
            .Where(r => r.Id == query.RepositoryId)
            .Select(r => r.Code)
            .FirstAsync(ct);

        var results = new List<SearchResultItemDto>();

        var tasks = await db.Set<SprintTask>().AsNoTracking()
            .Where(t => t.RepositoryId == query.RepositoryId &&
                        (t.Title.ToLower().Contains(term) || t.Description.ToLower().Contains(term)))
            .OrderByDescending(t => t.StateChangedAt)
            .Take(PerKindLimit)
            .Select(t => new { t.Id, t.Title, t.WorkItemNumber, t.Type })
            .ToListAsync(ct);
        results.AddRange(tasks.Select(t => new SearchResultItemDto(
            "task", t.Id, t.Title, $"{SprintTask.BuildWorkItemNumber(repoCode, t.WorkItemNumber)} · {t.Type}")));

        var backlog = await db.Set<BacklogItem>().AsNoTracking()
            .Where(b => b.RepositoryId == query.RepositoryId && b.Title.ToLower().Contains(term))
            .OrderBy(b => b.Rank)
            .Take(PerKindLimit)
            .Select(b => new { b.Id, b.Title, b.Type })
            .ToListAsync(ct);
        results.AddRange(backlog.Select(b => new SearchResultItemDto(
            "backlog", b.Id, b.Title, b.Type.ToString())));

        return results;
    }
}
