using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Commands.PromoteToSprint;

/// <summary>Handles <see cref="PromoteToSprintCommand"/>: validates the item is a Ready UserStory, creates the SprintTask, and marks the backlog item Committed.</summary>
public sealed class PromoteToSprintCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IHistoryService       historyService,
    IUnitOfWork           uow) : IRequestHandler<PromoteToSprintCommand, Result<Guid>>
{
    /// <summary>Validates state constraints, creates the sprint task linked to the backlog item, and commits.</summary>
    /// <param name="command">The promote command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created SprintTask.</returns>
    public async Task<Result<Guid>> Handle(PromoteToSprintCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.PromoteToSprint, ct))
            throw new ForbiddenException("You do not have permission to promote backlog items to a sprint.");

        var item = await db.Set<BacklogItem>().AsTracking()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.Id == command.ItemId && b.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(BacklogItem), command.ItemId);

        if (item.Type != BacklogItemType.UserStory)
            return Result<Guid>.Failure("Only UserStory backlog items can be promoted to a sprint.");

        if (item.State != BacklogItemState.Ready)
            return Result<Guid>.Failure($"Backlog item must be in 'Ready' state before promoting to a sprint. Current state: {item.State}.");

        if (!await db.Set<Sprint>().AnyAsync(s => s.Id == command.SprintId && s.RepositoryId == command.RepositoryId, ct))
            throw new NotFoundException(nameof(Sprint), command.SprintId);

        var maxNumber = await db.Set<SprintTask>().IgnoreQueryFilters()
            .Where(t => t.RepositoryId == command.RepositoryId)
            .MaxAsync(t => (int?)t.WorkItemNumber, ct) ?? 0;

        var sprintTask = new SprintTask
        {
            SprintId            = command.SprintId,
            RepositoryId        = command.RepositoryId,
            BacklogItemId       = command.ItemId,
            WorkItemNumber      = maxNumber + 1,
            Type                = SprintTaskType.UserStory,
            Title               = item.Title,
            State               = WorkItemState.Open,
            StoryPoints         = item.StoryPoints ?? 0,
            AcceptanceCriteria  = string.IsNullOrWhiteSpace(item.AcceptanceCriteria) ? null : item.AcceptanceCriteria,
            Documents           = [.. item.Documents],
            StateChangedAt      = DateTime.UtcNow,
        };
        db.Set<SprintTask>().Add(sprintTask);
        historyService.Record(sprintTask.Id, command.RepositoryId, user.UserId, "Created this User Story work item.");
        historyService.Record(sprintTask.Id, command.RepositoryId, user.UserId, $"Title: '{sprintTask.Title}'.");
        if (sprintTask.StoryPoints > 0)
            historyService.Record(sprintTask.Id, command.RepositoryId, user.UserId, $"Story points: {sprintTask.StoryPoints}.");

        item.State    = BacklogItemState.Committed;
        item.SprintId = command.SprintId;
        item.Touch();

        await uow.CommitAsync(ct);
        return Result<Guid>.Success(sprintTask.Id);
    }
}
