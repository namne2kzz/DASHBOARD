using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SmartBoard.Commands.DeleteColumn;

/// <summary>Handles <see cref="DeleteColumnCommand"/>: checks ManageBoard permission, removes the column, and closes the order gap.</summary>
public sealed class DeleteColumnCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<DeleteColumnCommand, Result>
{
    /// <summary>Validates permission, deletes the column, and re-indexes the remaining columns' order.</summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> when it is the last column.</returns>
    public async Task<Result> Handle(DeleteColumnCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageBoard, ct))
            throw new ForbiddenException("You do not have permission to manage the board in this repository.");

        var columns = await db.Set<SmartBoardColumn>()
            .AsTracking()
            .Where(c => c.RepositoryId == command.RepositoryId)
            .OrderBy(c => c.Order)
            .ToListAsync(ct);

        var column = columns.FirstOrDefault(c => c.Id == command.ColumnId)
            ?? throw new NotFoundException(nameof(SmartBoardColumn), command.ColumnId);

        // The board must always keep at least one column.
        if (columns.Count <= 1)
            return Result.Failure("Cannot delete the last column of the board.");

        db.Set<SmartBoardColumn>().Remove(column);

        // Close the order gap so the remaining columns stay contiguous (0,1,2,…).
        var order = 0;
        foreach (var remaining in columns.Where(c => c.Id != command.ColumnId))
            remaining.Order = order++;

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
