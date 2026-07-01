using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Wiki.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Wiki.Queries.ListWikiPages;

/// <summary>Handles <see cref="ListWikiPagesQuery"/>: validates membership, loads all pages flat, and builds the tree.</summary>
public sealed class ListWikiPagesQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListWikiPagesQuery, IReadOnlyList<WikiPageDto>>
{
    /// <summary>Validates membership, loads all pages, and returns the root-level tree with children.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Root-level pages with children populated recursively.</returns>
    public async Task<IReadOnlyList<WikiPageDto>> Handle(ListWikiPagesQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        var all = await db.Set<WikiPage>()
            .AsNoTracking()
            .Where(p => p.RepositoryId == query.RepositoryId)
            .OrderBy(p => p.Title)
            .ToListAsync(ct);

        return BuildTree(all, null);
    }

    private static IReadOnlyList<WikiPageDto> BuildTree(List<WikiPage> all, Guid? parentId)
    {
        return all
            .Where(p => p.ParentId == parentId)
            .Select(p => new WikiPageDto(
                p.Id, p.RepositoryId, p.Title, p.Content, p.LastUpdated,
                p.ParentId, p.CreatedAt, BuildTree(all, p.Id)))
            .ToList();
    }
}
