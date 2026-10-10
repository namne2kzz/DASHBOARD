using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.IntegrationEvents;

namespace DASHBOARD.Application.SprintTasks.Commands.ChangeSprintTaskState;

/// <summary>Handles <see cref="ChangeSprintTaskStateCommand"/>: applies state transition, stamps ClosedAt when Done, and notifies the assignee by email.</summary>
public sealed class ChangeSprintTaskStateCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IHistoryService       historyService,
    IUnitOfWork           uow,
    IPublishEndpoint      publisher,
    IEmailService         email,
    IAppSettings          settings,
    ILogger<ChangeSprintTaskStateCommandHandler> logger) : IRequestHandler<ChangeSprintTaskStateCommand, Result>
{
    private static readonly Dictionary<WorkItemState, string> StateLabels = new()
    {
        [WorkItemState.Open]       = "Open",
        [WorkItemState.ToDo]       = "To Do",
        [WorkItemState.InProgress] = "In Progress",
        [WorkItemState.InReview]   = "In Review",
        [WorkItemState.Verified]   = "Verified",
        [WorkItemState.Running]    = "Running",
        [WorkItemState.Done]       = "Done",
        [WorkItemState.Passed]     = "Passed",
        [WorkItemState.Failed]     = "Failed",
        [WorkItemState.Closed]     = "Closed",
    };

    /// <summary>Validates permission, applies the state transition, auto-zeroes remaining work on Done, commits, and emails the assignee.</summary>
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

        // Resolve metadata needed for the email before committing.
        var notifyUserId = task.AssignedToId;
        var changedById  = user.UserId;

        await uow.CommitAsync(ct);

        // State is part of the cached context HUB shows against a linked thread.
        await publisher.Publish(
            new DirectoryEntryChangedEvent(DirectoryEntryKind.WorkItem, command.TaskId), ct);

        // ── Email notification (best-effort — never fail the command) ──────────
        if (notifyUserId.HasValue && notifyUserId != changedById && oldState != command.NewState)
        {
            try
            {
                var notifyData = await (
                    from u in db.Set<User>().AsNoTracking()
                    where u.Id == notifyUserId
                    join r in db.Set<Repository>().AsNoTracking() on command.RepositoryId equals r.Id
                    join changer in db.Set<User>().AsNoTracking() on changedById equals changer.Id
                    select new { u.Email, u.Name, RepoName = r.Name, RepoCode = r.Code, ChangerName = changer.Name }
                ).FirstOrDefaultAsync(ct);

                if (notifyData is not null)
                {
                    var itemNumber = SprintTask.BuildWorkItemNumber(notifyData.RepoCode, task.WorkItemNumber);
                    var itemUrl    = $"{settings.InvitationFrontendBaseUrl.TrimEnd('/')}/boards/{itemNumber}";

                    await email.SendStateChangedAsync(
                        notifyData.Email, notifyData.Name,
                        itemNumber, task.Title,
                        StateLabels.GetValueOrDefault(oldState, oldState.ToString()),
                        StateLabels.GetValueOrDefault(command.NewState, command.NewState.ToString()),
                        notifyData.ChangerName, notifyData.RepoName, itemUrl, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "State-change email failed for task {TaskId}; notification skipped.", command.TaskId);
            }
        }

        return Result.Ok;
    }
}
