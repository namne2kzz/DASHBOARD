using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SmartBoard.Commands.ReorderColumns;

/// <summary>Handles <see cref="ReorderColumnsCommand"/>: validates that the submitted list covers all columns, then reassigns Order values.</summary>
public sealed class ReorderColumnsCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<ReorderColumnsCommand, Result>
{
    /// <summary>Validates permission and column completeness, then applies the new ordering.</summary>
    /// <param name="command">The reorder command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> when the column list is incomplete.</returns>
    public async Task<Result> Handle(ReorderColumnsCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageBoard, ct))
            return Result.Failure("You do not have permission to manage the board in this repository.");

        var columns = await db.Set<SmartBoardColumn>()
            .AsTracking()
            .Where(c => c.RepositoryId == command.RepositoryId)
            .ToListAsync(ct);

        if (columns.Count != command.OrderedColumnIds.Count ||
            command.OrderedColumnIds.Except(columns.Select(c => c.Id)).Any())
            return Result.Failure("The column ID list must include every column exactly once.");

        var indexMap = command.OrderedColumnIds
            .Select((id, i) => (id, i))
            .ToDictionary(x => x.id, x => x.i);

        foreach (var col in columns)
        {
            col.Order = indexMap[col.Id];
            col.Touch();
        }

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
