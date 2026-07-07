using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Commands.CreateSprintTask;

/// <summary>Handles <see cref="CreateSprintTaskCommand"/>: validates permissions, computes WorkItemNumber, creates the work item.</summary>
public sealed class CreateSprintTaskCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IHistoryService       historyService,
    IUnitOfWork           uow) : IRequestHandler<CreateSprintTaskCommand, Result<SprintTaskDto>>
{
    private static readonly Dictionary<SprintTaskType, string> TypeLabels = new()
    {
        [SprintTaskType.UserStory] = "User Story",
        [SprintTaskType.Task]      = "Task",
        [SprintTaskType.Bug]       = "Bug",
        [SprintTaskType.TestPlan]  = "Test Plan",
    };

    private static readonly Dictionary<WorkItemPriority, string> PriorityLabels = new()
    {
        [WorkItemPriority.Low]      = "Low",
        [WorkItemPriority.Medium]   = "Medium",
        [WorkItemPriority.High]     = "High",
        [WorkItemPriority.Critical] = "Critical",
    };

    /// <summary>Validates permission, assigns a per-repo sequence number, creates the work item, and returns the DTO.</summary>
    /// <param name="command">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="SprintTaskDto"/>, or a failure if permission is denied or business rules are violated.</returns>
    public async Task<Result<SprintTaskDto>> Handle(CreateSprintTaskCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.CreateWorkItem, ct))
            return Result<SprintTaskDto>.Failure("You do not have permission to create work items in this repository.");

        // UserStory must not have an assignee.
        if (command.Type == SprintTaskType.UserStory && command.AssignedToId.HasValue)
            return Result<SprintTaskDto>.Failure("User stories cannot be assigned to an individual. Assign sub-tasks instead.");

        // Validate sprint exists when provided.
        if (command.SprintId.HasValue &&
            !await db.Set<Sprint>().AnyAsync(s => s.Id == command.SprintId && s.RepositoryId == command.RepositoryId, ct))
            throw new NotFoundException(nameof(Sprint), command.SprintId.Value);

        // Validate parent task exists in the repo and is a User Story (sub-items nest under a Story only).
        (string Title, int WorkItemNumber)? parentInfo = null;
        if (command.ParentId.HasValue)
        {
            var parent = await db.Set<SprintTask>()
                .Where(t => t.Id == command.ParentId && t.RepositoryId == command.RepositoryId)
                .Select(t => new { t.Type, t.Title, t.WorkItemNumber })
                .FirstOrDefaultAsync(ct);
            if (parent is null)
                throw new NotFoundException(nameof(SprintTask), command.ParentId.Value);
            if (parent.Type != SprintTaskType.UserStory)
                return Result<SprintTaskDto>.Failure("Parent work item must be a User Story.");
            parentInfo = (parent.Title, parent.WorkItemNumber);
        }

        var repoCode = await db.Set<Repository>()
            .Where(r => r.Id == command.RepositoryId).Select(r => r.Code).FirstAsync(ct);

        // Compute next per-repo work item number.
        var maxNumber = await db.Set<SprintTask>().IgnoreQueryFilters()
            .Where(t => t.RepositoryId == command.RepositoryId)
            .MaxAsync(t => (int?)t.WorkItemNumber, ct) ?? 0;

        var initialState = command.SprintId.HasValue ? SprintTaskState.New : SprintTaskState.Backlog;

        var task = new SprintTask
        {
            SprintId         = command.SprintId,
            RepositoryId     = command.RepositoryId,
            ParentId         = command.ParentId,
            WorkItemNumber   = maxNumber + 1,
            Type             = command.Type,
            Title            = command.Title,
            Description      = command.Description,
            Priority         = command.Priority,
            AssignedToId     = command.AssignedToId,
            State            = initialState,
            StateChangedAt   = DateTime.UtcNow,
            StoryPoints      = command.StoryPoints,
            OriginalEstimate = command.OriginalEstimate,
            RemainingWork    = command.OriginalEstimate,
            CompletedWork    = 0m,
            StepsToReproduce = command.StepsToReproduce,
            Environment      = command.Environment,
            RootCause        = command.RootCause,
            Solution         = command.Solution,
            Impaction        = command.Impaction,
            UnitTest         = command.UnitTest,
            DesignReview     = command.DesignReview,
            TestSteps        = command.TestSteps,
            Automated        = command.Automated,
        };

        string? name = null, avatar = null;
        if (command.AssignedToId.HasValue)
        {
            var assignee = await db.Set<User>().AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == command.AssignedToId.Value, ct);
            name   = assignee?.Name;
            avatar = assignee?.AvatarClass;
        }

        db.Set<SprintTask>().Add(task);

        // Log creation as one row per populated field, so the audit trail shows exactly what
        // the item looked like at birth — same granularity as edits made later.
        historyService.Record(task.Id, command.RepositoryId, user.UserId,
            $"Created this {TypeLabels[task.Type]} work item.");
        historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Title: '{task.Title}'.");
        historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Priority: '{PriorityLabels[task.Priority]}'.");
        if (name is not null)
            historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Assigned to '{name}'.");
        if (parentInfo is not null)
            historyService.Record(task.Id, command.RepositoryId, user.UserId,
                $"Parent: '{SprintTask.BuildWorkItemNumber(repoCode, parentInfo.Value.WorkItemNumber)} {parentInfo.Value.Title}'.");
        if (task.Type == SprintTaskType.UserStory && task.StoryPoints > 0)
            historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Story points: {task.StoryPoints}.");
        if (task.Type is SprintTaskType.Task or SprintTaskType.Bug && task.OriginalEstimate > 0)
            historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Estimate: {task.OriginalEstimate}h.");
        if (!string.IsNullOrWhiteSpace(task.Description))
            historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Description: '{task.Description}'.");
        if (task.Type == SprintTaskType.Bug)
        {
            if (!string.IsNullOrWhiteSpace(task.StepsToReproduce)) historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Steps to reproduce: '{task.StepsToReproduce}'.");
            if (!string.IsNullOrWhiteSpace(task.Environment))      historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Environment: '{task.Environment}'.");
            if (!string.IsNullOrWhiteSpace(task.RootCause))        historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Root cause: '{task.RootCause}'.");
            if (!string.IsNullOrWhiteSpace(task.Solution))         historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Solution: '{task.Solution}'.");
            if (!string.IsNullOrWhiteSpace(task.Impaction))        historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Impact: '{task.Impaction}'.");
        }
        if (task.Type is SprintTaskType.Task or SprintTaskType.Bug)
        {
            if (!string.IsNullOrWhiteSpace(task.UnitTest))     historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Unit test notes: '{task.UnitTest}'.");
            if (!string.IsNullOrWhiteSpace(task.DesignReview)) historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Design review notes: '{task.DesignReview}'.");
        }
        if (task.Type == SprintTaskType.TestPlan)
        {
            if (task.TestSteps is { Count: > 0 })
                historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Test steps: '{string.Join("; ", task.TestSteps)}'.");
            historyService.Record(task.Id, command.RepositoryId, user.UserId, $"Automated: '{(task.Automated == true ? "on" : "off")}'.");
        }

        await uow.CommitAsync(ct);

        var parentWorkItemNumber = parentInfo is null ? null : SprintTask.BuildWorkItemNumber(repoCode, parentInfo.Value.WorkItemNumber);
        return Result<SprintTaskDto>.Success(MapToDto(task, repoCode, name, avatar, parentWorkItemNumber, parentInfo?.Title));
    }

    private static SprintTaskDto MapToDto(SprintTask t, string repoCode, string? assigneeName, string? assigneeAvatar,
        string? parentWorkItemNumber, string? parentTitle)
        => new(t.Id, t.SprintId, t.RepositoryId, t.BacklogItemId, t.ParentId, parentWorkItemNumber, parentTitle,
               SprintTask.BuildWorkItemNumber(repoCode, t.WorkItemNumber),
               t.Type, t.Title, t.Description, t.Priority,
               t.AssignedToId, assigneeName, assigneeAvatar,
               t.State, t.StoryPoints, t.OriginalEstimate, t.RemainingWork, t.CompletedWork, t.ClosedAt,
               t.StepsToReproduce, t.Environment, t.RootCause, t.Solution, t.Impaction,
               t.UnitTest, t.DesignReview, t.TestSteps, t.Automated, []);
}
