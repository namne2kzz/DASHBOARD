using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.WorkItemLinks.Queries.SearchLinkTargets;

/// <summary>Handles <see cref="SearchLinkTargetsQuery"/>: returns up to 20 work items matching the term, any type, excluding the current item.</summary>
public sealed class SearchLinkTargetsQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<SearchLinkTargetsQuery, IReadOnlyList<WorkItemPickerDto>>
{
    /// <summary>Validates membership and returns matching link-target candidates.</summary>
    /// <param name="query">The search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Up to 20 matching <see cref="WorkItemPickerDto"/>.</returns>
    public async Task<IReadOnlyList<WorkItemPickerDto>> Handle(SearchLinkTargetsQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var repoCode = await db.Set<Repository>()
            .Where(r => r.Id == query.RepositoryId).Select(r => r.Code).FirstAsync(ct);

        var items = await db.Set<SprintTask>()
            .AsNoTracking()
            .Where(t => t.RepositoryId == query.RepositoryId && t.Id != query.ExcludeTaskId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new { t.Id, t.WorkItemNumber, t.Title })
            .ToListAsync(ct);

        var term = query.SearchTerm?.Trim().ToLower();

        return items
            .Select(t => new WorkItemPickerDto(t.Id, SprintTask.BuildWorkItemNumber(repoCode, t.WorkItemNumber), t.Title))
            .Where(d => string.IsNullOrEmpty(term) ||
                        d.Title.ToLower().Contains(term) ||
                        d.WorkItemNumber.ToLower().Contains(term))
            .Take(20)
            .ToList();
    }
}
