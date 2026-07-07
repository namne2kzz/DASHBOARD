using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Application.SprintTasks.Queries.ListSprintTasks;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Queries.GetSprintTaskDetail;

/// <summary>Handles <see cref="GetSprintTaskDetailQuery"/>: loads one sprint task with its assignee for the detail dialog.</summary>
public sealed class GetSprintTaskDetailQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetSprintTaskDetailQuery, SprintTaskDto>
{
    /// <summary>Validates membership and returns the full task DTO (no sub-tasks — not needed by the detail dialog).</summary>
    /// <param name="query">The detail query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="SprintTaskDto"/> for the requested task.</returns>
    public async Task<SprintTaskDto> Handle(GetSprintTaskDetailQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        var task = await db.Set<SprintTask>()
            .AsNoTracking()
            .Include(t => t.AssignedTo)
            .Include(t => t.Parent)
            .FirstOrDefaultAsync(t => t.Id == query.TaskId && t.RepositoryId == query.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(SprintTask), query.TaskId);

        var repoCode = await db.Set<Repository>()
            .Where(r => r.Id == query.RepositoryId).Select(r => r.Code).FirstAsync(ct);

        var parentWorkItemNumber = task.Parent is null ? null : SprintTask.BuildWorkItemNumber(repoCode, task.Parent.WorkItemNumber);

        return ListSprintTasksQueryHandler.MapToDto(task, repoCode, [], parentWorkItemNumber, task.Parent?.Title);
    }
}
