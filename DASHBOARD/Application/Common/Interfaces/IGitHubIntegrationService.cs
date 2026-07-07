using DASHBOARD.Application.GitRepositories.DTOs;

namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>
/// Abstraction over live GitHub API calls for a single configured connection. The token for a call is
/// read from <c>GitConnectionEntry.Token</c> by the caller and passed in for the duration of that one
/// call only — never persisted beyond that scope, never logged.
/// </summary>
/// <remarks>
/// Phase 1 defines this contract only so downstream code (queries, DI) can compile against it.
/// The Octokit-backed implementation (<c>Infrastructure/Services/GitHub/OctokitGitHubIntegrationService.cs</c>)
/// and its DI registration are Phase 2 scope — do not implement or register it yet.
/// </remarks>
public interface IGitHubIntegrationService
{
    /// <summary>Validates that the token can reach the given repository (used to confirm a newly configured connection).</summary>
    /// <param name="ownerLogin">The GitHub owner/organization login.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="token">The Personal Access Token to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="GitHubValidationResultDto"/> describing whether the connection is reachable and valid.</returns>
    Task<GitHubValidationResultDto> ValidateAsync(string ownerLogin, string repoName, string token, CancellationToken ct);

    /// <summary>Lists the branches of the given repository.</summary>
    /// <param name="ownerLogin">The GitHub owner/organization login.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="token">The Personal Access Token to authenticate with.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The repository's branches.</returns>
    Task<IReadOnlyList<GitBranchDto>> ListBranchesAsync(string ownerLogin, string repoName, string token, CancellationToken ct);

    /// <summary>Lists recent commits for the given repository, optionally scoped to a single branch.</summary>
    /// <param name="ownerLogin">The GitHub owner/organization login.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="token">The Personal Access Token to authenticate with.</param>
    /// <param name="branch">Optional branch to scope the commit list to; <c>null</c> uses the default branch.</param>
    /// <param name="take">Maximum number of commits to return, bounding Octokit paging cost.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The most recent commits, newest first.</returns>
    Task<IReadOnlyList<GitCommitDto>> ListRecentCommitsAsync(string ownerLogin, string repoName, string token, string? branch, int take, CancellationToken ct);

    /// <summary>Lists pull requests for the given repository.</summary>
    /// <param name="ownerLogin">The GitHub owner/organization login.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="token">The Personal Access Token to authenticate with.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The repository's pull requests.</returns>
    Task<IReadOnlyList<GitPullRequestDto>> ListPullRequestsAsync(string ownerLogin, string repoName, string token, CancellationToken ct);

    /// <summary>Gets the current GitHub API rate-limit status for the given token.</summary>
    /// <param name="token">The Personal Access Token to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current rate-limit status.</returns>
    Task<GitRateLimitDto> GetRateLimitAsync(string token, CancellationToken ct);
}
