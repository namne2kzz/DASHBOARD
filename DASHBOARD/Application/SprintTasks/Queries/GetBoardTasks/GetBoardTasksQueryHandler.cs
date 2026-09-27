using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Queries.GetBoardTasks;

/// <summary>Handles <see cref="GetBoardTasksQuery"/>: returns a flat task list for the kanban board.</summary>
public sealed class GetBoardTasksQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetBoardTasksQuery, IReadOnlyList<BoardTaskDto>>
{
    /// <summary>Validates membership and returns all sprint tasks as a flat list, ordered by type then state.</summary>
    /// <param name="query">The board tasks query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Flat list of <see cref="BoardTaskDto"/> — includes Task, Bug, TestPlan, and UserStory items.</returns>
    public async Task<IReadOnlyList<BoardTaskDto>> Handle(GetBoardTasksQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var repoCode = await db.Set<Repository>()
            .Where(r => r.Id == query.RepositoryId).Select(r => r.Code).FirstAsync(ct);

        var tasks = await db.Set<SprintTask>()
            .AsNoTracking()
            .Where(t => t.SprintId == query.SprintId)
            .OrderBy(t => t.Type)
            .ThenBy(t => t.State)
            .ThenBy(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.WorkItemNumber,
                t.Type,
                t.Title,
                t.Priority,
                t.AssignedToId,
                AssignedToName   = t.AssignedTo != null ? t.AssignedTo.Name        : null,
                AssignedToAvatar = t.AssignedTo != null ? t.AssignedTo.AvatarClass : null,
                t.State,
                t.OriginalEstimate,
                t.RemainingWork,
                t.CompletedWork,
                t.StateChangedAt,
            })
            .ToListAsync(ct);

        var taskIds = tasks.Select(t => t.Id).ToList();

        // Assigned "Labels" catalog values per task, for card chips.
        var labels = await db.Set<WorkItemMetadata>().AsNoTracking()
            .Where(w => taskIds.Contains(w.SprintTaskId) && w.Metadata!.Key == MetadataKey.Labels)
            .Select(w => new { w.SprintTaskId, w.Metadata!.Value })
            .ToListAsync(ct);
        var labelsByTask = labels
            .GroupBy(l => l.SprintTaskId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Value).OrderBy(v => v).ToList());

        return tasks.Select(t => new BoardTaskDto(
            t.Id,
            SprintTask.BuildWorkItemNumber(repoCode, t.WorkItemNumber),
            t.Type,
            t.Title,
            t.Priority,
            t.AssignedToId,
            t.AssignedToName,
            t.AssignedToAvatar,
            t.State,
            t.OriginalEstimate,
            t.RemainingWork,
            t.CompletedWork,
            t.StateChangedAt,
            labelsByTask.TryGetValue(t.Id, out var ls) ? ls : (IReadOnlyList<string>)[])).ToList();
    }
}
