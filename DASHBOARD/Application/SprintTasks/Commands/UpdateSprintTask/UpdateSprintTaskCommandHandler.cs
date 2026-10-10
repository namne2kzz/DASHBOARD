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

namespace DASHBOARD.Application.SprintTasks.Commands.UpdateSprintTask;

/// <summary>Handles <see cref="UpdateSprintTaskCommand"/>: applies field updates to a work item and commits.</summary>
public sealed class UpdateSprintTaskCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IHistoryService       historyService,
    IUnitOfWork           uow,
    IPublishEndpoint      publisher,
    IEmailService         email,
    IAppSettings          settings,
    ILogger<UpdateSprintTaskCommandHandler> logger) : IRequestHandler<UpdateSprintTaskCommand, Result>
{
    private static readonly Dictionary<WorkItemPriority, string> PriorityLabels = new()
    {
        [WorkItemPriority.Low]      = "Low",
        [WorkItemPriority.Medium]   = "Medium",
        [WorkItemPriority.High]     = "High",
        [WorkItemPriority.Critical] = "Critical",
    };

    /// <summary>Validates permission, applies field updates, records one history entry per changed field, and persists.</summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(UpdateSprintTaskCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.EditWorkItem, ct))
            throw new ForbiddenException("You do not have permission to edit work items in this repository.");

        var task = await db.Set<SprintTask>().AsTracking()
            .FirstOrDefaultAsync(t => t.Id == command.TaskId && t.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(SprintTask), command.TaskId);

        var newAssignedToId = task.Type == SprintTaskType.UserStory ? null : command.AssignedToId;
        var changeMessages   = new List<string>();

        // Capture assignee change before mutation so we can email after commit.
        var oldAssignedToId = task.AssignedToId;
        var assigneeChanged = oldAssignedToId != newAssignedToId;
        var changedById     = user.UserId;

        SprintTask? newParent = null;
        if (command.ParentId.HasValue)
        {
            if (command.ParentId.Value == task.Id)
                return Result.Failure("A work item cannot be its own parent.");

            newParent = await db.Set<SprintTask>().AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == command.ParentId && t.RepositoryId == command.RepositoryId, ct)
                ?? throw new NotFoundException(nameof(SprintTask), command.ParentId.Value);
            if (newParent.Type != SprintTaskType.UserStory)
                return Result.Failure("Parent work item must be a User Story.");
        }

        if (task.ParentId != command.ParentId)
        {
            var repoCode = await db.Set<Repository>()
                .Where(r => r.Id == command.RepositoryId).Select(r => r.Code).FirstAsync(ct);

            var oldLabel = "None";
            if (task.ParentId.HasValue)
            {
                var old = await db.Set<SprintTask>().AsNoTracking()
                    .Where(t => t.Id == task.ParentId)
                    .Select(t => new { t.Title, t.WorkItemNumber })
                    .FirstOrDefaultAsync(ct);
                if (old is not null)
                    oldLabel = $"{SprintTask.BuildWorkItemNumber(repoCode, old.WorkItemNumber)} {old.Title}";
            }
            var newLabel = newParent is null ? "None" : $"{SprintTask.BuildWorkItemNumber(repoCode, newParent.WorkItemNumber)} {newParent.Title}";

            changeMessages.Add($"Parent changed from '{oldLabel}' to '{newLabel}'.");
        }

        // Short scalar fields: record the actual before/after values.
        if (task.Title != command.Title)
            changeMessages.Add($"Title changed from '{task.Title}' to '{command.Title}'.");
        if (task.Priority != command.Priority)
            changeMessages.Add($"Priority changed from '{PriorityLabels[task.Priority]}' to '{PriorityLabels[command.Priority]}'.");
        if (task.StoryPoints != command.StoryPoints)
            changeMessages.Add($"Story points changed from {task.StoryPoints} to {command.StoryPoints}.");
        if (task.OriginalEstimate != command.OriginalEstimate)
            changeMessages.Add($"Estimate changed from {task.OriginalEstimate}h to {command.OriginalEstimate}h.");
        if (task.Automated != command.Automated)
            changeMessages.Add($"Automated flag changed from {(task.Automated == true ? "on" : "off")} to {(command.Automated == true ? "on" : "off")}.");

        if (task.Description != command.Description)
            changeMessages.Add($"Description changed from '{task.Description}' to '{command.Description}'.");
        if (task.StepsToReproduce != command.StepsToReproduce)
            changeMessages.Add($"Steps to reproduce changed from '{task.StepsToReproduce}' to '{command.StepsToReproduce}'.");
        if (task.Environment != command.Environment)
            changeMessages.Add($"Environment changed from '{task.Environment}' to '{command.Environment}'.");
        if (task.RootCause != command.RootCause)
            changeMessages.Add($"Root cause changed from '{task.RootCause}' to '{command.RootCause}'.");
        if (task.Solution != command.Solution)
            changeMessages.Add($"Solution changed from '{task.Solution}' to '{command.Solution}'.");
        if (task.Impaction != command.Impaction)
            changeMessages.Add($"Impact changed from '{task.Impaction}' to '{command.Impaction}'.");
        if (task.UnitTest != command.UnitTest)
            changeMessages.Add($"Unit test notes changed from '{task.UnitTest}' to '{command.UnitTest}'.");
        if (task.DesignReview != command.DesignReview)
            changeMessages.Add($"Design review notes changed from '{task.DesignReview}' to '{command.DesignReview}'.");
        if (!(task.TestSteps ?? []).SequenceEqual(command.TestSteps ?? []))
            changeMessages.Add($"Test steps changed from '{string.Join("; ", task.TestSteps ?? [])}' to '{string.Join("; ", command.TestSteps ?? [])}'.");
        if (task.AcceptanceCriteria != command.AcceptanceCriteria)
            changeMessages.Add("Acceptance criteria updated.");
        if (!(task.Documents).SequenceEqual(command.Documents ?? []))
            changeMessages.Add("Documents updated.");
        var newRemaining = Math.Max(0, command.RemainingWork);
        if (task.RemainingWork != newRemaining)
            changeMessages.Add($"Remaining work changed from {task.RemainingWork}h to {newRemaining}h.");

        task.Title            = command.Title;
        task.Description      = command.Description;
        task.Priority         = command.Priority;
        task.AssignedToId     = newAssignedToId;
        task.ParentId         = command.ParentId;
        task.StoryPoints      = command.StoryPoints;
        task.OriginalEstimate = command.OriginalEstimate;
        task.StepsToReproduce = command.StepsToReproduce;
        task.Environment      = command.Environment;
        task.RootCause        = command.RootCause;
        task.Solution         = command.Solution;
        task.Impaction        = command.Impaction;
        task.UnitTest         = command.UnitTest;
        task.DesignReview     = command.DesignReview;
        task.TestSteps          = command.TestSteps;
        task.Automated          = command.Automated;
        task.AcceptanceCriteria = command.AcceptanceCriteria;
        task.Documents          = command.Documents ?? [];
        task.RemainingWork      = newRemaining;
        task.Touch();

        foreach (var message in changeMessages)
            historyService.Record(task.Id, command.RepositoryId, user.UserId, message);

        await uow.CommitAsync(ct);

        // HUB caches the title for discussion-thread headers; without this the thread keeps showing
        // the old title for up to two minutes.
        await publisher.Publish(
            new DirectoryEntryChangedEvent(DirectoryEntryKind.WorkItem, command.TaskId), ct);

        // ── Assignee-change email notifications (best-effort — never fail the command) ──
        if (assigneeChanged)
            await SendAssigneeEmailsAsync(
                command.RepositoryId, command.TaskId, task.WorkItemNumber, task.Title,
                oldAssignedToId, newAssignedToId, changedById, ct);

        return Result.Ok;
    }

    /// <summary>Sends assignment notifications to the old assignee (unassigned) and/or the new assignee (assigned).</summary>
    private async Task SendAssigneeEmailsAsync(
        Guid repositoryId, Guid taskId, int workItemNumber, string taskTitle,
        Guid? oldAssigneeId, Guid? newAssigneeId, Guid changedById, CancellationToken ct)
    {
        try
        {
            // Collect all user ids we need in a single round-trip.
            var userIds = new HashSet<Guid> { changedById };
            if (oldAssigneeId.HasValue) userIds.Add(oldAssigneeId.Value);
            if (newAssigneeId.HasValue) userIds.Add(newAssigneeId.Value);

            var users = await db.Set<User>().AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Email, u.Name })
                .ToDictionaryAsync(u => u.Id, ct);

            var repo = await db.Set<Repository>().AsNoTracking()
                .Where(r => r.Id == repositoryId)
                .Select(r => new { r.Name, r.Code })
                .FirstOrDefaultAsync(ct);

            if (repo is null) return;

            var itemNumber   = SprintTask.BuildWorkItemNumber(repo.Code, workItemNumber);
            var itemUrl      = $"{settings.InvitationFrontendBaseUrl.TrimEnd('/')}/boards/{itemNumber}";
            var changerName  = users.TryGetValue(changedById, out var changer) ? changer.Name : "Someone";

            // Notify old assignee that they were unassigned (skip if they made the change themselves).
            if (oldAssigneeId.HasValue && oldAssigneeId != changedById &&
                users.TryGetValue(oldAssigneeId.Value, out var oldUser))
            {
                await email.SendAssigneeChangedAsync(
                    oldUser.Email, oldUser.Name, itemNumber, taskTitle,
                    assigned: false, changerName, repo.Name, itemUrl, ct);
            }

            // Notify new assignee that they were assigned (skip if they made the change themselves).
            if (newAssigneeId.HasValue && newAssigneeId != changedById &&
                users.TryGetValue(newAssigneeId.Value, out var newUser))
            {
                await email.SendAssigneeChangedAsync(
                    newUser.Email, newUser.Name, itemNumber, taskTitle,
                    assigned: true, changerName, repo.Name, itemUrl, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Assignee-change email failed for task {TaskId}; notification skipped.", taskId);
        }
    }
}
