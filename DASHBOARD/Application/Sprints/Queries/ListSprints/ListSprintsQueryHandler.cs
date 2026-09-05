using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Sprints.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Sprints.Queries.ListSprints;

/// <summary>
/// Handles <see cref="ListSprintsQuery"/>: checks membership and returns all sprints ordered by start date,
/// including an optional HUB channel link per sprint.
/// </summary>
public sealed class ListSprintsQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListSprintsQuery, IReadOnlyList<SprintDto>>
{
    /// <summary>Validates membership and returns all sprint DTOs (with HUB channel links) ordered by descending start date.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All <see cref="SprintDto"/> records.</returns>
    public async Task<IReadOnlyList<SprintDto>> Handle(ListSprintsQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Left join Sprints → SprintChannelLinks to populate HUB channel info in one query
        var rows = await (
            from s in db.Set<Sprint>().AsNoTracking()
            where s.RepositoryId == query.RepositoryId
            join link in db.Set<SprintChannelLink>().AsNoTracking()
                on s.Id equals link.SprintId into links
            from link in links.DefaultIfEmpty()
            orderby s.StartDate descending
            select new { Sprint = s, Link = (SprintChannelLink?)link }
        ).ToListAsync(ct);

        return rows
            .Select(r => new SprintDto(
                r.Sprint.Id, r.Sprint.RepositoryId, r.Sprint.Name,
                r.Sprint.StartDate, r.Sprint.EndDate,
                r.Sprint.StartDate <= today && r.Sprint.EndDate >= today,
                r.Sprint.CreatedAt,
                r.Link == null ? null : r.Link.HubChannelId,
                r.Link == null ? null : r.Link.HubChannelUrl))
            .ToList();
    }
}
