using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Commands.ChangeSprintTaskState;

/// <summary>Handles <see cref="ChangeSprintTaskStateCommand"/>: applies state transition and stamps ClosedAt when Done.</summary>
public sealed class ChangeSprintTaskStateCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IHistoryService       historyService,
    IUnitOfWork           uow) : IRequestHandler<ChangeSprintTaskStateCommand, Result>
{
    /// <summary>Validates permission, applies the state transition, auto-zeroes remaining work on Done, and commits.</summary>
    /// <param name="command">The state change command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(ChangeSprintTaskStateCommand command, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(command.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var task = await db.Set<SprintTask>()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Id == command.TaskId && t.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(SprintTask), command.TaskId);

        var oldState = task.State;

        task.State          = command.NewState;
        task.StateChangedAt = DateTime.UtcNow;

        if (command.NewState == SprintTaskState.Done)
        {
            task.RemainingWork = 0;
            task.ClosedAt      = DateTime.UtcNow;
        }
        else
        {
            task.ClosedAt = null;
        }

        task.Touch();

        if (oldState != command.NewState)
            historyService.Record(task.Id, command.RepositoryId, user.UserId,
                $"State changed from '{oldState}' to '{command.NewState}'.");

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
