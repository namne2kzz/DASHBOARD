using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.MyWork.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.MyWork.Queries.GetMyWork;

/// <summary>
/// Handles <see cref="GetMyWorkQuery"/>: collects the current user's repository memberships, then
/// returns all non-Done work items assigned to them across those (non-archived) repositories.
/// </summary>
public sealed class GetMyWorkQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetMyWorkQuery, IReadOnlyList<MyWorkItemDto>>
{
    /// <summary>Runs the cross-repository assignment query for the authenticated user.</summary>
    /// <param name="query">The (parameterless) query marker.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Open work items assigned to the user, highest priority first.</returns>
    public async Task<IReadOnlyList<MyWorkItemDto>> Handle(GetMyWorkQuery query, CancellationToken ct)
    {
        var userId = user.UserId;

        // Repositories the user is a member of — the visibility boundary for their work.
        var repoIds = await db.Set<RepositoryMember>()
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.RepositoryId)
            .ToListAsync(ct);

        if (repoIds.Count == 0)
            return [];

        // Project to an intermediate shape first — the display work item number is built in memory
        // because BuildWorkItemNumber is a static helper EF Core cannot translate.
        var rows = await (
            from t in db.Set<SprintTask>().AsNoTracking()
            join r in db.Set<Repository>().AsNoTracking() on t.RepositoryId equals r.Id
            join sp in db.Set<Sprint>().AsNoTracking() on t.SprintId equals sp.Id into sprintGroup
            from sp in sprintGroup.DefaultIfEmpty()
            where t.AssignedToId == userId
               && repoIds.Contains(t.RepositoryId)
               && !r.IsArchived
               && t.State != WorkItemState.Done
               && t.State != WorkItemState.Passed
               && t.State != WorkItemState.Failed
               && t.State != WorkItemState.Closed
            orderby t.Priority descending, r.Code, t.State
            select new
            {
                t.Id, t.RepositoryId, RepoCode = r.Code, RepoName = r.Name,
                t.SprintId, SprintName = sp != null ? sp.Name : null,
                t.WorkItemNumber, t.Type, t.Title, t.Priority, t.State,
                t.StoryPoints, t.RemainingWork,
            }).ToListAsync(ct);

        return rows
            .Select(x => new MyWorkItemDto(
                x.Id, x.RepositoryId, x.RepoCode, x.RepoName,
                x.SprintId, x.SprintName,
                SprintTask.BuildWorkItemNumber(x.RepoCode, x.WorkItemNumber),
                x.Type, x.Title, x.Priority, x.State, x.StoryPoints, x.RemainingWork))
            .ToList();
    }
}
