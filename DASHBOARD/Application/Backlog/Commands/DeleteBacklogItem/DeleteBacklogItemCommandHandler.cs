using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Commands.DeleteBacklogItem;

/// <summary>Handles <see cref="DeleteBacklogItemCommand"/>: blocks deletion when children exist, then hard-deletes the item.</summary>
public sealed class DeleteBacklogItemCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<DeleteBacklogItemCommand, Result>
{
    /// <summary>Validates permission, checks for children, and deletes the item.</summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> when children block deletion.</returns>
    public async Task<Result> Handle(DeleteBacklogItemCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.DeleteWorkItem, ct))
            return Result.Failure("You do not have permission to delete backlog items in this repository.");

        var item = await db.Set<BacklogItem>()
            .FirstOrDefaultAsync(b => b.Id == command.ItemId && b.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(BacklogItem), command.ItemId);

        var hasChildren = await db.Set<BacklogItem>()
            .AnyAsync(b => b.ParentId == command.ItemId, ct);
        if (hasChildren)
            return Result.Failure("Cannot delete a backlog item that has children. Delete children first.");

        db.Set<BacklogItem>().Remove(item);
        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
