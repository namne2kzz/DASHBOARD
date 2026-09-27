using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Backlog.Commands.CreateBacklogItem;
using DASHBOARD.Application.Backlog.DTOs;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Queries.ListBacklogItems;

/// <summary>Handles <see cref="ListBacklogItemsQuery"/>: loads all backlog items flat then builds the hierarchy tree in memory.</summary>
public sealed class ListBacklogItemsQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListBacklogItemsQuery, IReadOnlyList<BacklogItemDto>>
{
    /// <summary>Validates membership, loads all items, and returns the root-level tree with children.</summary>
    /// <param name="query">The list query with optional filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Root-level items with their children recursively populated.</returns>
    public async Task<IReadOnlyList<BacklogItemDto>> Handle(ListBacklogItemsQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var all = await db.Set<BacklogItem>()
            .AsNoTracking()
            .Include(b => b.Sprint)
            .Where(b => b.RepositoryId == query.RepositoryId)
            .OrderBy(b => b.Rank)
            .ToListAsync(ct);

        if (query.TypeFilter.HasValue || query.StateFilter.HasValue)
            all = KeepMatchesWithTheirAncestors(all, query.TypeFilter, query.StateFilter);

        return BuildTree(all, null);
    }

    /// <summary>
    /// Narrows the flat list to the items matching the filters, then adds back every ancestor of a
    /// match so the hierarchy stays whole.
    /// </summary>
    /// <remarks>
    /// Filtering the flat list alone would drop the Epic and Feature above a matching User Story,
    /// leaving the story with no parent to hang from — it would vanish from the result entirely.
    /// The backlog is always presented as Epic → Feature → User Story, and a story is never shown
    /// orphaned, so an ancestor is kept for context even when it does not match the filter itself.
    /// </remarks>
    /// <param name="all">Every backlog item in the repository, ordered by rank.</param>
    /// <param name="typeFilter">Optional type to match.</param>
    /// <param name="stateFilter">Optional state to match.</param>
    /// <returns>The matches plus their ancestors, still ordered by rank.</returns>
    private static List<BacklogItem> KeepMatchesWithTheirAncestors(
        List<BacklogItem>  all,
        BacklogItemType?   typeFilter,
        BacklogItemState?  stateFilter)
    {
        var byId = all.ToDictionary(b => b.Id);

        var keep = new HashSet<Guid>();
        foreach (var item in all)
        {
            if (typeFilter.HasValue  && item.Type  != typeFilter.Value)  continue;
            if (stateFilter.HasValue && item.State != stateFilter.Value) continue;

            keep.Add(item.Id);

            // Walk up to the root. The guard stops a corrupted parent chain from looping forever.
            var cursor = item.ParentId;
            var guard  = 0;
            while (cursor is { } parentId && byId.TryGetValue(parentId, out var parent) && guard++ < 100)
            {
                if (!keep.Add(parent.Id)) break;   // this ancestor chain is already accounted for
                cursor = parent.ParentId;
            }
        }

        return all.Where(b => keep.Contains(b.Id)).ToList();
    }

    private static IReadOnlyList<BacklogItemDto> BuildTree(List<BacklogItem> all, Guid? parentId)
    {
        return all
            .Where(b => b.ParentId == parentId)
            .Select(b => CreateBacklogItemCommandHandler.ToDto(b, BuildTree(all, b.Id)))
            .ToList();
    }
}
