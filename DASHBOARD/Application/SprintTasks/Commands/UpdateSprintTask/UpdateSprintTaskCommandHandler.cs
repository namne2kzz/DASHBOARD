using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Commands.UpdateSprintTask;

/// <summary>Handles <see cref="UpdateSprintTaskCommand"/>: applies field updates to a work item and commits.</summary>
public sealed class UpdateSprintTaskCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateSprintTaskCommand, Result>
{
    /// <summary>Validates permission, applies field updates, and persists the changes.</summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(UpdateSprintTaskCommand command, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(command.RepositoryId, ct))
            return Result.Failure("You are not a member of this repository.");

        var task = await db.Set<SprintTask>().AsTracking()
            .FirstOrDefaultAsync(t => t.Id == command.TaskId && t.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(SprintTask), command.TaskId);

        //if (task.Type == SprintTaskType.UserStory && command.AssignedToId.HasValue)
        //    throw new ValidationException([new ValidationFailure("AssignedToId",
        //        "User stories cannot be assigned to an individual.")]);

        task.Title            = command.Title;
        task.Description      = command.Description;
        task.Priority         = command.Priority;
        task.AssignedToId     = task.Type == SprintTaskType.UserStory ? null : command.AssignedToId;
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

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
