using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Commands.UpdateSprintTask;

/// <summary>Handles <see cref="UpdateSprintTaskCommand"/>: applies field updates to a work item and commits.</summary>
public sealed class UpdateSprintTaskCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IHistoryService       historyService,
    IUnitOfWork           uow) : IRequestHandler<UpdateSprintTaskCommand, Result>
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
            return Result.Failure("You do not have permission to edit work items in this repository.");

        var task = await db.Set<SprintTask>().AsTracking()
            .FirstOrDefaultAsync(t => t.Id == command.TaskId && t.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(SprintTask), command.TaskId);

        var newAssignedToId = task.Type == SprintTaskType.UserStory ? null : command.AssignedToId;
        var changeMessages   = new List<string>();

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
        task.TestSteps        = command.TestSteps;
        task.Automated        = command.Automated;
        task.Touch();

        foreach (var message in changeMessages)
            historyService.Record(task.Id, command.RepositoryId, user.UserId, message);

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
