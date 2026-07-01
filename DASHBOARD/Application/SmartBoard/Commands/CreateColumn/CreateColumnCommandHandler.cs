using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SmartBoard.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SmartBoard.Commands.CreateColumn;

/// <summary>Handles <see cref="CreateColumnCommand"/>: checks ManageBoard permission, appends at the end of the column order, and persists.</summary>
public sealed class CreateColumnCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<CreateColumnCommand, SmartBoardColumnDto>
{
    /// <summary>Validates permission, assigns the next order index, creates the column, and returns the DTO.</summary>
    /// <param name="command">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="SmartBoardColumnDto"/>.</returns>
    public async Task<SmartBoardColumnDto> Handle(CreateColumnCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageBoard, ct))
            throw new UnauthorizedAccessException("You do not have permission to manage the board in this repository.");

        var maxOrder = await db.Set<SmartBoardColumn>()
            .AsNoTracking()
            .Where(c => c.RepositoryId == command.RepositoryId)
            .MaxAsync(c => (int?)c.Order, ct) ?? -1;

        var column = new SmartBoardColumn
        {
            RepositoryId   = command.RepositoryId,
            Name           = command.Name,
            MappedState    = command.MappedState,
            WipLimit       = command.WipLimit,
            WipMode        = command.WipMode,
            AgingLimitDays = command.AgingLimitDays,
            Order          = maxOrder + 1,
        };
        db.Set<SmartBoardColumn>().Add(column);
        await uow.CommitAsync(ct);

        return new SmartBoardColumnDto(column.Id, column.RepositoryId, column.Name, column.MappedState,
            column.WipLimit, column.WipMode, column.AgingLimitDays, column.Order);
    }
}
