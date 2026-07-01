using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Commands.CreateSprintTask;

/// <summary>Handles <see cref="CreateSprintTaskCommand"/>: validates permissions, computes WorkItemNumber, creates the work item.</summary>
public sealed class CreateSprintTaskCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IHistoryService       historyService,
    IUnitOfWork           uow) : IRequestHandler<CreateSprintTaskCommand, SprintTaskDto>
{
    /// <summary>Validates permission, assigns a per-repo sequence number, creates the work item, and returns the DTO.</summary>
    /// <param name="command">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="SprintTaskDto"/>.</returns>
    public async Task<SprintTaskDto> Handle(CreateSprintTaskCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageSprint, ct))
            throw new UnauthorizedAccessException("You do not have permission to create work items in this repository.");

        // UserStory must not have an assignee.
        if (command.Type == SprintTaskType.UserStory && command.AssignedToId.HasValue)
            throw new ValidationException([new ValidationFailure("AssignedToId",
                "User stories cannot be assigned to an individual. Assign sub-tasks instead.")]);

        // Validate sprint exists when provided.
        if (command.SprintId.HasValue &&
            !await db.Set<Sprint>().AnyAsync(s => s.Id == command.SprintId && s.RepositoryId == command.RepositoryId, ct))
            throw new NotFoundException(nameof(Sprint), command.SprintId.Value);

        // Validate parent task exists in the repo and is a User Story (sub-items nest under a Story only).
        if (command.ParentId.HasValue)
        {
            var parentType = await db.Set<SprintTask>()
                .Where(t => t.Id == command.ParentId && t.RepositoryId == command.RepositoryId)
                .Select(t => (SprintTaskType?)t.Type)
                .FirstOrDefaultAsync(ct);
            if (parentType is null)
                throw new NotFoundException(nameof(SprintTask), command.ParentId.Value);
            if (parentType != SprintTaskType.UserStory)
                throw new ValidationException([new ValidationFailure("ParentId", "Parent work item must be a User Story.")]);
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
        historyService.Record(task.Id, command.RepositoryId, user.UserId,
            name is not null ? $"Work item created. Assigned to '{name}'." : "Work item created.");
        await uow.CommitAsync(ct);

        return MapToDto(task, repoCode, name, avatar);
    }

    private static SprintTaskDto MapToDto(SprintTask t, string repoCode, string? assigneeName, string? assigneeAvatar)
        => new(t.Id, t.SprintId, t.RepositoryId, t.BacklogItemId, t.ParentId, SprintTask.BuildWorkItemNumber(repoCode, t.WorkItemNumber),
               t.Type, t.Title, t.Description, t.Priority,
               t.AssignedToId, assigneeName, assigneeAvatar,
               t.State, t.StoryPoints, t.OriginalEstimate, t.RemainingWork, t.CompletedWork, t.ClosedAt,
               t.StepsToReproduce, t.Environment, t.RootCause, t.Solution, t.Impaction,
               t.UnitTest, t.DesignReview, t.TestSteps, t.Automated, []);
}
