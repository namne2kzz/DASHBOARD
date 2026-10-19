using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Helpers;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DASHBOARD.Application.SprintTasks.Commands.AssignSprintTask;

/// <summary>Handles <see cref="AssignSprintTaskCommand"/>: updates the assignee on a sprint task and notifies the affected users.</summary>
public sealed class AssignSprintTaskCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IHistoryService       historyService,
    INotificationService  notifications,
    IEmailService         email,
    IAppSettings          settings,
    ILogger<AssignSprintTaskCommandHandler> logger,
    IUnitOfWork           uow) : IRequestHandler<AssignSprintTaskCommand, Result>
{
    /// <summary>Validates membership, verifies the assignee is a repo member, updates the task, commits, and notifies.</summary>
    /// <param name="command">The assign command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(AssignSprintTaskCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.AssignWorkItem, ct))
            throw new ForbiddenException("You do not have permission to assign work items in this repository.");

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
                !await db.Set<RepositoryMember>()
                    .AnyAsync(m => m.RepositoryId == command.RepositoryId && m.UserId == command.AssignedToId, ct))
                return Result.Failure("The assigned user is not a member of this repository.");
            assigneeName = assignee.Name;
        }

        var oldAssigneeId = task.AssignedToId;
        var newAssigneeId = command.AssignedToId;
        var changed       = oldAssigneeId != newAssigneeId;

        if (changed)
            historyService.Record(task.Id, command.RepositoryId, user.UserId,
                assigneeName is not null ? $"Assigned to '{assigneeName}'." : "Unassigned.");

        task.AssignedToId = command.AssignedToId;
        task.Touch();

        await uow.CommitAsync(ct);

        if (changed)
            await NotifyAssigneeChangeAsync(task, command.RepositoryId, oldAssigneeId, newAssigneeId, ct);

        return Result.Ok;
    }

    /// <summary>Emails and push-notifies the old and new assignee about the change (best-effort — never fails the command).</summary>
    private async Task NotifyAssigneeChangeAsync(
        SprintTask task, Guid repositoryId, Guid? oldAssigneeId, Guid? newAssigneeId, CancellationToken ct)
    {
        try
        {
            var changedById = user.UserId;

            var notifyIds = new List<Guid>();
            if (oldAssigneeId is { } o && o != changedById) notifyIds.Add(o);
            if (newAssigneeId is { } n && n != changedById) notifyIds.Add(n);
            if (notifyIds.Count == 0) return;

            var repo = await db.Set<Repository>().AsNoTracking()
                .Where(r => r.Id == repositoryId)
                .Select(r => new { r.Code, r.Name })
                .FirstOrDefaultAsync(ct);
            if (repo is null) return;

            var users = await db.Set<User>().AsNoTracking()
                .Where(u => notifyIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => new { u.Name, u.Email }, ct);

            var changerName = users.TryGetValue(changedById, out var changer)
                ? changer.Name
                : (await db.Set<User>().AsNoTracking().Where(u => u.Id == changedById).Select(u => u.Name).FirstOrDefaultAsync(ct) ?? "Someone");

            var itemNumber = SprintTask.BuildWorkItemNumber(repo.Code, task.WorkItemNumber);
            var itemUrl    = $"{settings.InvitationFrontendBaseUrl.TrimEnd('/')}/boards/{itemNumber}";
            var prefs      = await NotifyPrefsHelper.GetBatchForTypeAsync(db, notifyIds, NotificationType.WorkItemAssigned, ct);

            // Old assignee → unassigned.
            if (oldAssigneeId is { } oldId && oldId != changedById && users.TryGetValue(oldId, out var oldUser))
            {
                var (emailOn, pushOn) = prefs.TryGetValue(oldId, out var p) ? p : (true, false);
                if (emailOn)
                    await email.SendAssigneeChangedAsync(oldUser.Email, oldUser.Name, itemNumber, task.Title,
                        assigned: false, changerName, repo.Name, itemUrl, ct);
                if (pushOn)
                    await notifications.NotifyAsync(oldId, NotificationType.WorkItemUnassigned,
                        $"Unassigned from {itemNumber}", task.Title, itemUrl, ct);
            }

            // New assignee → assigned.
            if (newAssigneeId is { } newId && newId != changedById && users.TryGetValue(newId, out var newUser))
            {
                var (emailOn, pushOn) = prefs.TryGetValue(newId, out var p) ? p : (true, false);
                if (emailOn)
                    await email.SendAssigneeChangedAsync(newUser.Email, newUser.Name, itemNumber, task.Title,
                        assigned: true, changerName, repo.Name, itemUrl, ct);
                if (pushOn)
                    await notifications.NotifyAsync(newId, NotificationType.WorkItemAssigned,
                        $"Assigned to {itemNumber}", task.Title, itemUrl, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Assignee-change notification failed for task {TaskId}.", task.Id);
        }
    }
}
