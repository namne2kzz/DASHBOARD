using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Queries.GetSprintTaskMetadata;

/// <summary>Handles <see cref="GetSprintTaskMetadataQuery"/>: lists a work item's assigned catalog values with their key names.</summary>
public sealed class GetSprintTaskMetadataQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetSprintTaskMetadataQuery, IReadOnlyList<WorkItemMetadataDto>>
{
    /// <summary>Validates membership and returns the work item's metadata assignments, ordered by key then value.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assigned metadata values.</returns>
    public async Task<IReadOnlyList<WorkItemMetadataDto>> Handle(GetSprintTaskMetadataQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        var rows = await db.Set<WorkItemMetadata>().AsNoTracking()
            .Where(w => w.SprintTaskId == query.SprintTaskId)
            .Select(w => new { w.MetadataId, w.Metadata!.Key, w.Metadata.Value })
            .OrderBy(w => w.Key).ThenBy(w => w.Value)
            .ToListAsync(ct);

        return rows
            .Select(r => new WorkItemMetadataDto(r.MetadataId, (int)r.Key, r.Key.GetDisplayName(), r.Value))
            .ToList();
    }
}
