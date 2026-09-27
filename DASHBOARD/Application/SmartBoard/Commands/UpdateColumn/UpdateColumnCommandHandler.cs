using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SmartBoard.Commands.UpdateColumn;

/// <summary>Handles <see cref="UpdateColumnCommand"/>: checks ManageBoard permission then applies configuration changes.</summary>
public sealed class UpdateColumnCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateColumnCommand, Result>
{
    /// <summary>Validates permission, applies configuration and state-mapping changes, and commits.</summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(UpdateColumnCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageBoard, ct))
            throw new ForbiddenException("You do not have permission to manage the board in this repository.");

        var column = await db.Set<SmartBoardColumn>()
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == command.ColumnId && c.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(SmartBoardColumn), command.ColumnId);

        column.Name           = command.Name;
        column.MappedState    = command.MappedState;
        column.WipLimit       = command.WipLimit;
        column.WipMode        = command.WipMode;
        column.AgingLimitDays = command.AgingLimitDays;
        column.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
