using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Queries.ResolveSprintTaskKey;

/// <summary>
/// Handles <see cref="ResolveSprintTaskKeyQuery"/>: parses the formatted work-item key
/// (e.g. "DASH-10") and returns the matching sprint task UUID.
/// </summary>
public sealed class ResolveSprintTaskKeyQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ResolveSprintTaskKeyQuery, Guid>
{
    /// <summary>Validates membership, parses the key, and resolves it to a task UUID.</summary>
    /// <param name="query">The resolve query containing the formatted key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The UUID of the sprint task matching the key.</returns>
    public async Task<Guid> Handle(ResolveSprintTaskKeyQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        // Parse the numeric suffix: "DASH-10" → 10, "10" → 10.
        var segments = query.Key.Trim().Split('-');
        if (!int.TryParse(segments[^1], out var number) || number <= 0)
            throw new NotFoundException(nameof(SprintTask), query.Key);

        var taskId = await db.Set<SprintTask>()
            .Where(t => t.RepositoryId == query.RepositoryId && t.WorkItemNumber == number)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);

        if (taskId is null)
            throw new NotFoundException(nameof(SprintTask), query.Key);

        return taskId.Value;
    }
}
