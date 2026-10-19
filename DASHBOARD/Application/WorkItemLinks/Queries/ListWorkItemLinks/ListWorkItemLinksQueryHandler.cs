using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.WorkItemLinks.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.WorkItemLinks.Queries.ListWorkItemLinks;

/// <summary>
/// Handles <see cref="ListWorkItemLinksQuery"/>: returns every link touching the work item, with the
/// label oriented from this item's perspective (outward when this item is the source, inward otherwise).
/// </summary>
public sealed class ListWorkItemLinksQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListWorkItemLinksQuery, IReadOnlyList<WorkItemLinkDto>>
{
    /// <summary>Validates membership, loads both-direction links, and projects them to the item's view.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Links involving the item, ordered by type then key.</returns>
    public async Task<IReadOnlyList<WorkItemLinkDto>> Handle(ListWorkItemLinksQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var repoCode = await db.Set<Repository>().AsNoTracking()
            .Where(r => r.Id == query.RepositoryId).Select(r => r.Code).FirstOrDefaultAsync(ct)
            ?? string.Empty;

        var links = await db.Set<WorkItemLink>().AsNoTracking()
            .Where(l => l.RepositoryId == query.RepositoryId &&
                        (l.SourceTaskId == query.SprintTaskId || l.TargetTaskId == query.SprintTaskId))
            .Select(l => new
            {
                l.Id,
                l.LinkType,
                l.Description,
                IsOutward = l.SourceTaskId == query.SprintTaskId,
                // Project the "other" item's scalar fields directly so EF can translate the
                // ternary (selecting a whole navigation entity through a ternary does not materialize).
                OtherId     = l.SourceTaskId == query.SprintTaskId ? l.TargetTask!.Id : l.SourceTask!.Id,
                OtherNumber = l.SourceTaskId == query.SprintTaskId ? l.TargetTask!.WorkItemNumber : l.SourceTask!.WorkItemNumber,
                OtherTitle  = l.SourceTaskId == query.SprintTaskId ? l.TargetTask!.Title : l.SourceTask!.Title,
                OtherType   = l.SourceTaskId == query.SprintTaskId ? l.TargetTask!.Type : l.SourceTask!.Type,
                OtherState  = l.SourceTaskId == query.SprintTaskId ? l.TargetTask!.State : l.SourceTask!.State,
            })
            .ToListAsync(ct);

        return links
            .Select(l => new WorkItemLinkDto(
                l.Id,
                l.LinkType.ToString(),
                WorkItemLinkLabels.For(l.LinkType, l.IsOutward),
                l.OtherId,
                SprintTask.BuildWorkItemNumber(repoCode, l.OtherNumber),
                l.OtherTitle,
                l.OtherType.ToString(),
                l.OtherState.ToString(),
                l.Description))
            .OrderBy(d => d.Label)
            .ThenBy(d => d.TargetKey)
            .ToList();
    }
}
