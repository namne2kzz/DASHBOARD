using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Wiki.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Wiki.Queries.GetWikiPage;

/// <summary>Handles <see cref="GetWikiPageQuery"/>: validates membership, loads the page with direct children, and returns the DTO.</summary>
public sealed class GetWikiPageQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetWikiPageQuery, WikiPageDto>
{
    /// <summary>Validates membership, loads the page and its direct children, and returns the DTO.</summary>
    /// <param name="query">The get query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="WikiPageDto"/> with direct children.</returns>
    public async Task<WikiPageDto> Handle(GetWikiPageQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        var page = await db.Set<WikiPage>().AsNoTracking()
            .Include(p => p.Children)
            .FirstOrDefaultAsync(p => p.Id == query.PageId && p.RepositoryId == query.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(WikiPage), query.PageId);

        var children = page.Children
            .Select(c => new WikiPageDto(c.Id, c.RepositoryId, c.Title, c.Content, c.LastUpdated, c.ParentId, c.CreatedAt, []))
            .ToList();

        return new WikiPageDto(page.Id, page.RepositoryId, page.Title, page.Content, page.LastUpdated, page.ParentId, page.CreatedAt, children);
    }
}
