using DASHBOARD.Application.SmartBoard.DTOs;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.SmartBoard.Commands.CreateColumn;

/// <summary>Creates a new column on the smart board. Requires <see cref="SystemFunction.ManageBoard"/>.</summary>
public sealed record CreateColumnCommand(
    Guid            RepositoryId,
    string          Name,
    SprintTaskState MappedState,
    int             WipLimit,
    WipMode         WipMode,
    int             AgingLimitDays) : IRequest<SmartBoardColumnDto>;
