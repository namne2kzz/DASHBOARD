using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Commands.DescodeSprintTask;

/// <summary>Handles <see cref="DescodeSprintTaskCommand"/>: deletes the sprint task tree and restores the originating backlog item to Ready.</summary>
public sealed class DescodeSprintTaskCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<DescodeSprintTaskCommand, Result>
{
    /// <summary>Validates permission, removes the task and its sub-tasks, restores the backlog item state, and commits.</summary>
    /// <param name="command">The descope command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(DescodeSprintTaskCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.PromoteToSprint, ct))
            return Result.Failure("You do not have permission to descope sprint tasks.");

        var task = await db.Set<SprintTask>()
            .AsTracking()
            .Include(t => t.SubTasks)
            .FirstOrDefaultAsync(t => t.Id == command.TaskId && t.SprintId == command.SprintId, ct)
            ?? throw new NotFoundException(nameof(SprintTask), command.TaskId);

        if (task.ParentId != null)
            return Result.Failure("Only root-level tasks (user stories) can be descoped.");

        // Delete sub-tasks first (Restrict cascade requires manual ordering).
        db.Set<SprintTask>().RemoveRange(task.SubTasks);
        db.Set<SprintTask>().Remove(task);

        // Restore the originating backlog item to Ready so it can be re-planned.
        if (task.BacklogItemId.HasValue)
        {
            var backlogItem = await db.Set<BacklogItem>().AsTracking()
                .FirstOrDefaultAsync(b => b.Id == task.BacklogItemId, ct);

            if (backlogItem is not null)
            {
                backlogItem.State = BacklogItemState.Ready;
                backlogItem.Touch();
            }
        }

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}