namespace DASHBOARD.Application.GitRepositories.DTOs;

/// <summary>A single branch of a connected GitHub repository.</summary>
public sealed record GitBranchDto(
    string   Name,
    bool     IsDefault,
    string   LastCommitSha,
    DateTime LastCommitAt,
    bool     IsStale);
