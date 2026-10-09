using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationEvents;

namespace DASHBOARD.Application.SprintTasks.Commands.ChangeSprintTaskState;

/// <summary>Handles <see cref="ChangeSprintTaskStateCommand"/>: applies state transition and stamps ClosedAt when Done.</summary>
public sealed class ChangeSprintTaskStateCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IHistoryService       historyService,
    IUnitOfWork           uow,
    IPublishEndpoint      publisher) : IRequestHandler<ChangeSprintTaskStateCommand, Result>
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

        // Validate transition is allowed for this work item type.
        if (!SprintTask.AllowedStates(task.Type).Contains(command.NewState))
            return Result.Failure($"State '{command.NewState}' is not valid for {task.Type}.");

        var oldState = task.State;

        task.State          = command.NewState;
        task.StateChangedAt = DateTime.UtcNow;

        if (task.Category == StateCategory.Done)
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

        // State is part of the cached context HUB shows against a linked thread.
        await publisher.Publish(
            new DirectoryEntryChangedEvent(DirectoryEntryKind.WorkItem, command.TaskId), ct);

        return Result.Ok;
    }
}
