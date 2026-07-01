using DASHBOARD.Application.Backlog.Commands.CreateBacklogItem;
using DASHBOARD.Application.Backlog.DTOs;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
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
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        var all = await db.Set<BacklogItem>()
            .AsNoTracking()
            .Include(b => b.Sprint)
            .Where(b => b.RepositoryId == query.RepositoryId)
            .OrderBy(b => b.Rank)
            .ToListAsync(ct);

        // Apply optional filters to the flat list before tree building.
        if (query.TypeFilter.HasValue)
            all = all.Where(b => b.Type == query.TypeFilter.Value).ToList();
        if (query.StateFilter.HasValue)
            all = all.Where(b => b.State == query.StateFilter.Value).ToList();

        return BuildTree(all, null);
    }

    private static IReadOnlyList<BacklogItemDto> BuildTree(List<BacklogItem> all, Guid? parentId)
    {
        return all
            .Where(b => b.ParentId == parentId)
            .Select(b => CreateBacklogItemCommandHandler.ToDto(b, BuildTree(all, b.Id)))
            .ToList();
    }
}
