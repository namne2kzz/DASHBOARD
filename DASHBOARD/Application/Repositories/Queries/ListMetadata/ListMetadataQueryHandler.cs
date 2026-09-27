using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Repositories.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Repositories.Queries.ListMetadata;

/// <summary>Handles <see cref="ListMetadataQuery"/>: checks membership, applies optional key filter, and returns the catalog entries.</summary>
public sealed class ListMetadataQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListMetadataQuery, IReadOnlyList<RepositoryMetadataDto>>
{
    /// <summary>Validates membership, filters by key when supplied, and returns entries ordered by value.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Active catalog entries ordered by key then value.</returns>
    public async Task<IReadOnlyList<RepositoryMetadataDto>> Handle(ListMetadataQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        // Global entries (shared across repos) plus entries owned by this repository.
        var q = db.Set<RepositoryMetadata>()
            .AsNoTracking()
            .Where(m => m.IsGlobal || m.RepositoryId == query.RepositoryId);

        if (query.Key.HasValue)
            q = q.Where(m => m.Key == query.Key.Value);

        // GetDisplayName() calls reflection — project in-memory after the DB round-trip.
        var rows = await q
            .OrderBy(m => m.Key)
            .ThenBy(m => m.Value)
            .ToListAsync(ct);

        return rows
            .Select(m => new RepositoryMetadataDto(m.Id, m.RepositoryId, m.IsGlobal, m.Key.ToString(), m.Key.GetDisplayName(), m.Value, m.CreatedAt))
            .ToList();
    }
}
