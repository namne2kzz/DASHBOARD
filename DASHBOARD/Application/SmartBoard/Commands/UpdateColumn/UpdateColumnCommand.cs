using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.SmartBoard.Commands.UpdateColumn;

/// <summary>Updates the configuration and state mapping of an existing board column.</summary>
public sealed record UpdateColumnCommand(
    Guid            RepositoryId,
    Guid            ColumnId,
    string          Name,
    SprintTaskState MappedState,
    int             WipLimit,
    WipMode         WipMode,
    int             AgingLimitDays) : IRequest<Result>;
