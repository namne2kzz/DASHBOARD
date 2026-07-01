using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.Backlog.DTOs;

/// <summary>Snapshot of a product backlog item.</summary>
public sealed record BacklogItemDto(
    Guid             Id,
    Guid             RepositoryId,
    BacklogItemType  Type,
    string           Title,
    Guid?            ParentId,
    decimal          Rank,
    Guid?            SprintId,
    string?          SprintName,
    BacklogItemState State,
    int?             StoryPoints,
    TshirtSize?      TshirtSize,
    string           AcceptanceCriteria,
    IReadOnlyList<string> Documents,
    DateTime         CreatedAt,
    IReadOnlyList<BacklogItemDto> Children);
