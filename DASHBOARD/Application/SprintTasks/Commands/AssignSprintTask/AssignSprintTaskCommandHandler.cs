using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Commands.AssignSprintTask;

/// <summary>Handles <see cref="AssignSprintTaskCommand"/>: updates the assignee on a sprint task.</summary>
public sealed class AssignSprintTaskCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IHistoryService       historyService,
    IUnitOfWork           uow) : IRequestHandler<AssignSprintTaskCommand, Result>
{
    /// <summary>Validates membership, verifies the assignee is a repo member, updates the task, and commits.</summary>
    /// <param name="command">The assign command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(AssignSprintTaskCommand command, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(command.RepositoryId, ct))
            return Result.Failure("You are not a member of this repository.");

        var task = await db.Set<SprintTask>()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Id == command.TaskId && t.SprintId == command.SprintId, ct)
            ?? throw new NotFoundException(nameof(SprintTask), command.TaskId);

        string? assigneeName = null;

        // Verify the target user is actually a member of this repo (skip check when unassigning).
        if (command.AssignedToId.HasValue)
        {
            var assignee = await db.Set<User>()
                .FirstOrDefaultAsync(u => u.Id == command.AssignedToId, ct);
            if (assignee is null ||
                !await db.Set<Domain.Entities.RepositoryMember>()
                    .AnyAsync(m => m.RepositoryId == command.RepositoryId && m.UserId == command.AssignedToId, ct))
                return Result.Failure("The assigned user is not a member of this repository.");
            assigneeName = assignee.Name;
        }

        if (task.AssignedToId != command.AssignedToId)
            historyService.Record(task.Id, command.RepositoryId, user.UserId,
                assigneeName is not null ? $"Assigned to '{assigneeName}'." : "Unassigned.");

        task.AssignedToId = command.AssignedToId;
        task.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}