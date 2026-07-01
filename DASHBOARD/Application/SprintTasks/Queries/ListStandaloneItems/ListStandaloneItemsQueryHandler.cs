using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Queries.ListStandaloneItems;

/// <summary>Handles <see cref="ListStandaloneItemsQuery"/>: returns root-level Bug/TestPlan items with no sprint assignment.</summary>
public sealed class ListStandaloneItemsQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListStandaloneItemsQuery, IReadOnlyList<SprintTaskSummaryDto>>
{
    /// <summary>Validates membership and returns standalone work item summaries.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of <see cref="SprintTaskSummaryDto"/> for standalone items.</returns>
    public async Task<IReadOnlyList<SprintTaskSummaryDto>> Handle(ListStandaloneItemsQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        var repoCode = await db.Set<Repository>()
            .Where(r => r.Id == query.RepositoryId).Select(r => r.Code).FirstAsync(ct);

        var q = db.Set<SprintTask>()
            .AsNoTracking()
            .Include(t => t.AssignedTo)
            .Where(t => t.RepositoryId == query.RepositoryId && t.SprintId == null && t.ParentId == null);

        if (query.Type.HasValue)
            q = q.Where(t => t.Type == query.Type);
        if (query.State.HasValue)
            q = q.Where(t => t.State == query.State);

        var items = await q.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);

        return items.Select(t => new SprintTaskSummaryDto(
            t.Id, t.SprintId, t.RepositoryId, SprintTask.BuildWorkItemNumber(repoCode, t.WorkItemNumber), t.Type, t.Title,
            t.Priority, t.AssignedToId, t.AssignedTo?.Name, t.AssignedTo?.AvatarClass,
            t.State, t.StoryPoints, t.OriginalEstimate, t.RemainingWork,
            t.SubTasks.Count(s => !s.IsDeleted))).ToList();
    }
}
