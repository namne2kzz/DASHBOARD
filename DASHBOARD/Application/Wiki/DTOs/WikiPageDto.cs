namespace DASHBOARD.Application.Wiki.DTOs;

/// <summary>Wiki page snapshot with optional tree of children.</summary>
public sealed record WikiPageDto(
    Guid     Id,
    Guid     RepositoryId,
    string   Title,
    string   Content,
    DateTime LastUpdated,
    Guid?    ParentId,
    DateTime CreatedAt,
    IReadOnlyList<WikiPageDto> Children);
