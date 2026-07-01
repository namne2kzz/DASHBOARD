namespace DASHBOARD.Application.Repositories.DTOs;

/// <summary>A single selectable metadata value entry (global or repository-scoped).</summary>
/// <remarks><paramref name="Key"/> is the enum name (e.g. "RepoRole") so the client matches it as a stable string.</remarks>
public sealed record RepositoryMetadataDto(
    Guid     Id,
    Guid?    RepositoryId,
    bool     IsGlobal,
    string   Key,
    string   DisplayName,
    string   Value,
    DateTime CreatedAt);
