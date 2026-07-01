namespace DASHBOARD.Application.Repositories.DTOs;

/// <summary>Summary of a project repository returned by list and get queries.</summary>
public sealed record RepositoryDto(
    Guid    Id,
    string  Name,
    string  Code,
    string  Description,
    int     MemberCount,
    DateTime CreatedAt);
