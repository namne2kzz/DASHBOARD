using DASHBOARD.Application.Backlog.DTOs;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Commands.BulkUpdateBacklogState;

/// <summary>
/// Handles <see cref="BulkUpdateBacklogStateCommand"/>: transitions every selected backlog item to the
/// target refinement state in one commit. Mirrors the single-item rules — Committed transitions are rejected.
/// </summary>
public sealed class BulkUpdateBacklogStateCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<BulkUpdateBacklogStateCommand, Result<BulkOperationResultDto>>
{
    /// <summary>Validates permission and target state, applies the state to all matching items, and commits once.</summary>
    /// <param name="command">The bulk state transition command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with an affected/skipped summary; failure on permission or business-rule violation.</returns>
    public async Task<Result<BulkOperationResultDto>> Handle(BulkUpdateBacklogStateCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageBacklog, ct))
            return Result<BulkOperationResultDto>.Failure("You do not have permission to manage backlog items in this repository.");

        if (command.State == BacklogItemState.Committed)
            return Result<BulkOperationResultDto>.Failure("Use 'Move to Iteration' or 'Promote to Sprint' to commit backlog items.");

        var ids = command.ItemIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result<BulkOperationResultDto>.Success(new BulkOperationResultDto(0, 0));

        var items = await db.Set<BacklogItem>().AsTracking()
            .Where(b => ids.Contains(b.Id) && b.RepositoryId == command.RepositoryId)
            .ToListAsync(ct);

        // Committed items are locked to a sprint — skip them rather than silently reverting the promotion.
        var updatable = items.Where(b => b.State != BacklogItemState.Committed).ToList();
        foreach (var item in updatable)
        {
            item.State = command.State;
            item.Touch();
        }

        if (updatable.Count > 0)
            await uow.CommitAsync(ct);

        return Result<BulkOperationResultDto>.Success(
            new BulkOperationResultDto(updatable.Count, ids.Count - updatable.Count));
    }
}
