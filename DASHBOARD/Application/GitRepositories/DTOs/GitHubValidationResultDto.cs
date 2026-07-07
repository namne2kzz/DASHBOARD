namespace DASHBOARD.Application.GitRepositories.DTOs;

/// <summary>
/// Result of validating a configured GitHub connection (reachable, token accepted, repo exists).
/// Produced by <see cref="Common.Interfaces.IGitHubIntegrationService.ValidateAsync"/> — implemented in Phase 2.
/// </summary>
public sealed record GitHubValidationResultDto(
    bool    IsValid,
    string? DefaultBranch,
    string? ErrorMessage);
