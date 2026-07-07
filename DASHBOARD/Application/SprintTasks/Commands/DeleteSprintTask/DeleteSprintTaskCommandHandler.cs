using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Commands.DeleteSprintTask;

/// <summary>Handles <see cref="DeleteSprintTaskCommand"/>: soft-deletes the item and its sub-tasks if it is a root UserStory.</summary>
public sealed class DeleteSprintTaskCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<DeleteSprintTaskCommand, Result>
{
    /// <summary>Validates permission, soft-deletes the item (and sub-tasks for UserStory), and commits.</summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(DeleteSprintTaskCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.DeleteWorkItem, ct))
            return Result.Failure("You do not have permission to delete work items in this repository.");

        var task = await db.Set<SprintTask>()
            .Include(t => t.SubTasks)
            .FirstOrDefaultAsync(t => t.Id == command.TaskId && t.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(SprintTask), command.TaskId);

        var now    = DateTime.UtcNow;
        var userId = user.UserId;

        SoftDelete(task, now, userId);
        foreach (var sub in task.SubTasks)
            SoftDelete(sub, now, userId);

        // If this was a UserStory promoted from backlog, restore the backlog item to Ready.
        if (task.Type == SprintTaskType.UserStory && task.BacklogItemId.HasValue)
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

    private static void SoftDelete(SprintTask t, DateTime now, Guid userId)
    {
        t.IsDeleted       = true;
        t.DeletedAt       = now;
        t.DeletedByUserId = userId;
        t.Touch();
    }
}
