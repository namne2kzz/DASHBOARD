using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogState;

/// <summary>Handles <see cref="UpdateBacklogStateCommand"/>: transitions a backlog item's refinement state. Blocks transitions to <see cref="BacklogItemState.Committed"/> (use Promote to Sprint instead).</summary>
public sealed class UpdateBacklogStateCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateBacklogStateCommand, Result>
{
    /// <summary>Validates permission and business rules, applies the new state, and commits.</summary>
    /// <param name="command">The state transition command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; failure result on permission or business rule violation.</returns>
    public async Task<Result> Handle(UpdateBacklogStateCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.EditWorkItem, ct))
            return Result.Failure("You do not have permission to edit backlog items in this repository.");

        if (command.State == BacklogItemState.Committed)
            return Result.Failure("Use 'Move to Iteration' or 'Promote to Sprint' to commit a backlog item.");

        var item = await db.Set<BacklogItem>().AsTracking()
            .FirstOrDefaultAsync(b => b.Id == command.ItemId && b.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(BacklogItem), command.ItemId);

        item.State = command.State;
        item.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
