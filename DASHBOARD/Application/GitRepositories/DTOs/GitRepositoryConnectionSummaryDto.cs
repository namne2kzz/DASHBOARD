namespace DASHBOARD.Application.GitRepositories.DTOs;

/// <summary>A configured GitHub connection for a project, with credentials stripped. Never includes the access token.</summary>
public sealed record GitRepositoryConnectionSummaryDto(
    string RepoUrl,
    string OwnerLogin,
    string RepoName,
    bool   IsPrimary);
