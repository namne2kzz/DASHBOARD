using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogItem;

/// <summary>Handles <see cref="UpdateBacklogItemCommand"/>: checks edit permission then applies the field updates.</summary>
public sealed class UpdateBacklogItemCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateBacklogItemCommand, Result>
{
    /// <summary>Validates permission, applies changes, and commits.</summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(UpdateBacklogItemCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageBacklog, ct))
            return Result.Failure("You do not have permission to manage backlog items in this repository.");

        if (command.State == BacklogItemState.Committed)
            return Result.Failure("Cannot set state to 'Committed' directly. Use 'Promote to Sprint' to commit a backlog item.");

        var item = await db.Set<BacklogItem>()
            .AsTracking()
            .FirstOrDefaultAsync(b => b.Id == command.ItemId && b.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(BacklogItem), command.ItemId);

        item.Title       = command.Title;
        item.State       = command.State;
        item.SprintId    = command.SprintId;
        item.StoryPoints = command.StoryPoints;
        item.TshirtSize         = command.TshirtSize;
        item.AcceptanceCriteria = command.AcceptanceCriteria;
        item.Documents          = [.. command.Documents];
        item.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
