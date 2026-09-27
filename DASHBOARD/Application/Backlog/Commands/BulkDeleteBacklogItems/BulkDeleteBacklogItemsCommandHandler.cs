using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Backlog.DTOs;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Commands.BulkDeleteBacklogItems;

/// <summary>
/// Handles <see cref="BulkDeleteBacklogItemsCommand"/>: hard-deletes the selected backlog items in one commit.
/// An item is skipped when it has children that are not themselves part of the selection, preserving the
/// single-item rule that a parent may not be removed while descendants remain.
/// </summary>
public sealed class BulkDeleteBacklogItemsCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<BulkDeleteBacklogItemsCommand, Result<BulkOperationResultDto>>
{
    /// <summary>Validates permission, filters out items that would orphan children, deletes the rest, and commits once.</summary>
    /// <param name="command">The bulk delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with a deleted/skipped summary; failure on permission violation.</returns>
    public async Task<Result<BulkOperationResultDto>> Handle(BulkDeleteBacklogItemsCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageBacklog, ct))
            throw new ForbiddenException("You do not have permission to manage backlog items in this repository.");

        var ids = command.ItemIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result<BulkOperationResultDto>.Success(new BulkOperationResultDto(0, 0));

        var items = await db.Set<BacklogItem>().AsTracking()
            .Where(b => ids.Contains(b.Id) && b.RepositoryId == command.RepositoryId)
            .ToListAsync(ct);

        // Parents that still own at least one child outside the selection would be orphaned — skip them.
        var blockedParentIds = (await db.Set<BacklogItem>()
                .Where(b => b.ParentId != null && ids.Contains(b.ParentId.Value) && !ids.Contains(b.Id))
                .Select(b => b.ParentId!.Value)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        var deletable = items.Where(b => !blockedParentIds.Contains(b.Id)).ToList();
        if (deletable.Count > 0)
        {
            db.Set<BacklogItem>().RemoveRange(deletable);
            await uow.CommitAsync(ct);
        }

        // Skipped is measured against the ids the caller asked for, so Affected + Skipped always
        // equals the selection size. An id that matches no row — already deleted, or belonging to
        // another repository — counts as skipped rather than vanishing from both figures.
        return Result<BulkOperationResultDto>.Success(
            new BulkOperationResultDto(deletable.Count, ids.Count - deletable.Count));
    }
}
