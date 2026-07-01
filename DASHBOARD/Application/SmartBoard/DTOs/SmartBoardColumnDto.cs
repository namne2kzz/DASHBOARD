using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.SmartBoard.DTOs;

/// <summary>Smart board column snapshot including WIP configuration and state mapping.</summary>
public sealed record SmartBoardColumnDto(
    Guid            Id,
    Guid            RepositoryId,
    string          Name,
    SprintTaskState MappedState,
    int             WipLimit,
    WipMode         WipMode,
    int             AgingLimitDays,
    int             Order);
