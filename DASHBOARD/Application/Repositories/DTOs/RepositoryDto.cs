namespace DASHBOARD.Application.Repositories.DTOs;

/// <summary>Summary of a project repository returned by list and get queries.</summary>
/// <param name="Id">Repository identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Code">Short code. Forms the URL segment and work-item key prefix, so it never changes.</param>
/// <param name="Description">Free-text description.</param>
/// <param name="MemberCount">How many users belong to the repository.</param>
/// <param name="CreatedAt">When the repository was created.</param>
/// <param name="IsArchived">
/// Whether the repository is archived. Archived repositories are hidden from ordinary views but
/// kept intact, so an administration screen can list and restore them.
/// </param>
/// <param name="LeadId">
/// The Project Manager's user id, or null when nobody holds the role. Named for the position
/// rather than the role so renaming the role later does not ripple through the API.
/// </param>
/// <param name="LeadName">The Project Manager's display name, or null when nobody holds the role.</param>
public sealed record RepositoryDto(
    Guid    Id,
    string  Name,
    string  Code,
    string  Description,
    int     MemberCount,
    DateTime CreatedAt,
    bool    IsArchived = false,
    Guid?   LeadId = null,
    string? LeadName = null);
