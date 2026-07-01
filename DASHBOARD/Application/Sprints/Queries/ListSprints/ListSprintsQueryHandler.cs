using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Sprints.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Sprints.Queries.ListSprints;

/// <summary>Handles <see cref="ListSprintsQuery"/>: checks membership and returns all sprints ordered by start date.</summary>
public sealed class ListSprintsQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListSprintsQuery, IReadOnlyList<SprintDto>>
{
    /// <summary>Validates membership and returns all sprint DTOs ordered by descending start date.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All <see cref="SprintDto"/> records.</returns>
    public async Task<IReadOnlyList<SprintDto>> Handle(ListSprintsQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var sprints = await db.Set<Sprint>()
            .AsNoTracking()
            .Where(s => s.RepositoryId == query.RepositoryId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);

        return sprints
            .Select(s => new SprintDto(s.Id, s.RepositoryId, s.Name, s.StartDate, s.EndDate,
                s.StartDate <= today && s.EndDate >= today, s.CreatedAt))
            .ToList();
    }
}
