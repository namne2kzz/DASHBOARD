using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.GitRepositories.DTOs;
using Octokit;

namespace DASHBOARD.Infrastructure.Services.GitHub;

/// <summary>
/// <see cref="IGitHubIntegrationService"/> implementation backed by the Octokit GitHub API client.
/// A fresh <see cref="GitHubClient"/> is created for every call, credentialed with the token passed in
/// for that call only — tokens are never cached, persisted beyond the call, or logged by this service.
/// </summary>
/// <remarks>
/// Octokit's REST client does not accept a <see cref="CancellationToken"/> on its own API calls, so
/// cancellation is only honored at the start of each method (before any request is issued) rather than
/// mid-flight — a known limitation of the underlying library, not this service.
/// </remarks>
public sealed class OctokitGitHubIntegrationService : IGitHubIntegrationService
{
    /// <summary>Upper bound on branches fetched per call, to bound Octokit paging cost.</summary>
    private const int BranchWindow = 100;

    /// <summary>Upper bound on pull requests fetched per call, to bound Octokit paging cost.</summary>
    private const int PullRequestWindow = 50;

    /// <summary>Validates that the token can reach the given repository (used to confirm a newly configured connection).</summary>
    /// <param name="ownerLogin">The GitHub owner/organization login.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="token">The Personal Access Token to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="GitHubValidationResultDto"/> describing whether the connection is reachable and valid.</returns>
    /// <exception cref="GitRateLimitExceededException">Thrown when GitHub's rate limit is exhausted for this token.</exception>
    /// <exception cref="GitProviderUnavailableException">Thrown for any other unexpected failure reaching GitHub.</exception>
    public async Task<GitHubValidationResultDto> ValidateAsync(string ownerLogin, string repoName, string token, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var client = CreateClient(token);

        try
        {
            var repository = await client.Repository.Get(ownerLogin, repoName);
            return new GitHubValidationResultDto(true, repository.DefaultBranch, null);
        }
        catch (Octokit.NotFoundException)
        {
            return new GitHubValidationResultDto(false, null, "Repository not found or token lacks access.");
        }
        catch (Octokit.AuthorizationException)
        {
            return new GitHubValidationResultDto(false, null, "Token rejected by GitHub.");
        }
        catch (Octokit.RateLimitExceededException ex)
        {
            throw new GitRateLimitExceededException(ex.Reset, ex);
        }
        catch (Exception ex)
        {
            throw new GitProviderUnavailableException("GitHub was unreachable while validating the connection.", ex);
        }
    }

    /// <summary>Lists the branches of the given repository.</summary>
    /// <param name="ownerLogin">The GitHub owner/organization login.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="token">The Personal Access Token to authenticate with.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The repository's branches.</returns>
    /// <exception cref="GitCredentialInvalidException">Thrown when the repository is not found or the token is rejected.</exception>
    /// <exception cref="GitRateLimitExceededException">Thrown when GitHub's rate limit is exhausted for this token.</exception>
    /// <exception cref="GitProviderUnavailableException">Thrown for any other unexpected failure reaching GitHub.</exception>
    public async Task<IReadOnlyList<GitBranchDto>> ListBranchesAsync(string ownerLogin, string repoName, string token, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var client = CreateClient(token);

        try
        {
            var repository = await client.Repository.Get(ownerLogin, repoName);
            var branches = await client.Repository.Branch.GetAll(
                ownerLogin, repoName, new ApiOptions { PageSize = BranchWindow, PageCount = 1 });

            // Octokit's Branch.Commit only carries a SHA (no date/author), so one lightweight commit
            // lookup per branch is needed to populate LastCommitAt/IsStale. Bounded by BranchWindow above
            // and refreshed at most once per cache TTL by GetGitRepositoryOverviewQueryHandler's read-through cache.
            var withCommits = await Task.WhenAll(branches.Select(async branch =>
            {
                var commit = await client.Repository.Commit.Get(ownerLogin, repoName, branch.Commit.Sha);
                return (Branch: branch, Commit: commit);
            }));

            var now = DateTimeOffset.UtcNow;

            return withCommits
                .Select(x =>
                {
                    var lastCommitAt = x.Commit.Commit.Committer?.Date ?? x.Commit.Commit.Author?.Date ?? now;
                    return new GitBranchDto(
                        Name: x.Branch.Name,
                        IsDefault: string.Equals(x.Branch.Name, repository.DefaultBranch, StringComparison.Ordinal),
                        LastCommitSha: x.Branch.Commit.Sha,
                        LastCommitAt: lastCommitAt.UtcDateTime,
                        IsStale: now - lastCommitAt > TimeSpan.FromDays(90));
                })
                .ToList();
        }
        catch (Octokit.NotFoundException ex)
        {
            throw new GitCredentialInvalidException("Repository not found or token lacks access.", ex);
        }
        catch (Octokit.AuthorizationException ex)
        {
            throw new GitCredentialInvalidException("Token rejected by GitHub.", ex);
        }
        catch (Octokit.RateLimitExceededException ex)
        {
            throw new GitRateLimitExceededException(ex.Reset, ex);
        }
        catch (Exception ex)
        {
            throw new GitProviderUnavailableException("GitHub was unreachable while listing branches.", ex);
        }
    }

    /// <summary>Lists recent commits for the given repository, optionally scoped to a single branch.</summary>
    /// <param name="ownerLogin">The GitHub owner/organization login.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="token">The Personal Access Token to authenticate with.</param>
    /// <param name="branch">Optional branch to scope the commit list to; <c>null</c> uses the default branch.</param>
    /// <param name="take">Maximum number of commits to return, bounding Octokit paging cost.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The most recent commits, newest first.</returns>
    /// <exception cref="GitCredentialInvalidException">Thrown when the repository is not found or the token is rejected.</exception>
    /// <exception cref="GitRateLimitExceededException">Thrown when GitHub's rate limit is exhausted for this token.</exception>
    /// <exception cref="GitProviderUnavailableException">Thrown for any other unexpected failure reaching GitHub.</exception>
    public async Task<IReadOnlyList<GitCommitDto>> ListRecentCommitsAsync(
        string ownerLogin, string repoName, string token, string? branch, int take, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var client = CreateClient(token);

        try
        {
            var request = branch is null ? new CommitRequest() : new CommitRequest { Sha = branch };
            var commits = await client.Repository.Commit.GetAll(
                ownerLogin, repoName, request, new ApiOptions { PageSize = take, PageCount = 1 });

            return commits
                .Take(take)
                .Select(c =>
                {
                    var date = c.Commit.Author?.Date ?? c.Commit.Committer?.Date ?? DateTimeOffset.UtcNow;
                    return new GitCommitDto(
                        Sha: c.Sha,
                        ShortSha: c.Sha.Length >= 7 ? c.Sha[..7] : c.Sha,
                        Branch: branch ?? string.Empty,
                        Message: c.Commit.Message,
                        AuthorName: c.Commit.Author?.Name ?? c.Author?.Login ?? "unknown",
                        AuthorEmail: c.Commit.Author?.Email ?? string.Empty,
                        Date: date.UtcDateTime,
                        LinkedWorkItemId: null); // resolved by GetGitRepositoryOverviewQueryHandler, not this service
                })
                .ToList();
        }
        catch (Octokit.NotFoundException ex)
        {
            throw new GitCredentialInvalidException("Repository not found or token lacks access.", ex);
        }
        catch (Octokit.AuthorizationException ex)
        {
            throw new GitCredentialInvalidException("Token rejected by GitHub.", ex);
        }
        catch (Octokit.RateLimitExceededException ex)
        {
            throw new GitRateLimitExceededException(ex.Reset, ex);
        }
        catch (Exception ex)
        {
            throw new GitProviderUnavailableException("GitHub was unreachable while listing commits.", ex);
        }
    }

    /// <summary>Lists pull requests for the given repository.</summary>
    /// <param name="ownerLogin">The GitHub owner/organization login.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="token">The Personal Access Token to authenticate with.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The repository's pull requests (open, closed, and merged).</returns>
    /// <exception cref="GitCredentialInvalidException">Thrown when the repository is not found or the token is rejected.</exception>
    /// <exception cref="GitRateLimitExceededException">Thrown when GitHub's rate limit is exhausted for this token.</exception>
    /// <exception cref="GitProviderUnavailableException">Thrown for any other unexpected failure reaching GitHub.</exception>
    public async Task<IReadOnlyList<GitPullRequestDto>> ListPullRequestsAsync(string ownerLogin, string repoName, string token, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var client = CreateClient(token);

        try
        {
            var request = new PullRequestRequest { State = ItemStateFilter.All };
            var pullRequests = await client.PullRequest.GetAllForRepository(
                ownerLogin, repoName, request, new ApiOptions { PageSize = PullRequestWindow, PageCount = 1 });

            return pullRequests.Select(MapPullRequest).ToList();
        }
        catch (Octokit.NotFoundException ex)
        {
            throw new GitCredentialInvalidException("Repository not found or token lacks access.", ex);
        }
        catch (Octokit.AuthorizationException ex)
        {
            throw new GitCredentialInvalidException("Token rejected by GitHub.", ex);
        }
        catch (Octokit.RateLimitExceededException ex)
        {
            throw new GitRateLimitExceededException(ex.Reset, ex);
        }
        catch (Exception ex)
        {
            throw new GitProviderUnavailableException("GitHub was unreachable while listing pull requests.", ex);
        }
    }

    /// <summary>Gets the current GitHub API rate-limit status for the given token.</summary>
    /// <param name="token">The Personal Access Token to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current rate-limit status for the core REST API.</returns>
    /// <exception cref="GitCredentialInvalidException">Thrown when the token is rejected.</exception>
    /// <exception cref="GitRateLimitExceededException">Thrown when GitHub's rate limit is exhausted for this token.</exception>
    /// <exception cref="GitProviderUnavailableException">Thrown for any other unexpected failure reaching GitHub.</exception>
    public async Task<GitRateLimitDto> GetRateLimitAsync(string token, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var client = CreateClient(token);

        try
        {
            var rateLimits = await client.RateLimit.GetRateLimits();
            var core = rateLimits.Resources.Core;
            return new GitRateLimitDto(core.Limit, core.Remaining, core.Reset.UtcDateTime);
        }
        catch (Octokit.AuthorizationException ex)
        {
            throw new GitCredentialInvalidException("Token rejected by GitHub.", ex);
        }
        catch (Octokit.RateLimitExceededException ex)
        {
            throw new GitRateLimitExceededException(ex.Reset, ex);
        }
        catch (Exception ex)
        {
            throw new GitProviderUnavailableException("GitHub was unreachable while checking the rate limit.", ex);
        }
    }

    /// <summary>Maps an Octokit <see cref="PullRequest"/> to <see cref="GitPullRequestDto"/> on a best-effort basis.</summary>
    /// <param name="pr">The Octokit pull request model.</param>
    /// <returns>The mapped DTO, with <c>LinkedWorkItemId</c> left <c>null</c> for the caller to resolve.</returns>
    /// <remarks>
    /// Octokit's REST client has no single "review decision" field — that is a GitHub GraphQL concept
    /// (<c>reviewDecision</c>), not exposed by the REST Pull Requests API this service uses. <see cref="GitPullRequestDto.ReviewDecision"/>
    /// is therefore derived only from <c>Merged</c>/<c>State</c> ("Merged" / "Closed" / "Unknown" for still-open PRs), and
    /// <see cref="GitPullRequestDto.Reviewers"/> lists <em>requested</em> reviewers rather than reviewers who have actually
    /// submitted a review — getting the real review state would require one extra Pull Request Reviews API call per PR
    /// (<c>client.PullRequest.Review.GetAll</c>), which this method intentionally avoids to keep this call's cost bounded.
    /// </remarks>
    private static GitPullRequestDto MapPullRequest(PullRequest pr)
    {
        var status = pr.Merged ? "merged" : pr.State.Value == ItemState.Closed ? "closed" : "open";
        var reviewDecision = pr.Merged ? "Merged" : pr.State.Value == ItemState.Closed ? "Closed" : "Unknown";

        return new GitPullRequestDto(
            Number: pr.Number,
            Title: pr.Title,
            SourceBranch: pr.Head.Ref,
            TargetBranch: pr.Base.Ref,
            Status: status,
            ReviewDecision: reviewDecision,
            AuthorLogin: pr.User?.Login ?? "unknown",
            Reviewers: pr.RequestedReviewers.Select(u => u.Login).ToList(),
            LinkedWorkItemId: null, // resolved by GetGitRepositoryOverviewQueryHandler, not this service
            MergedAt: pr.MergedAt?.UtcDateTime);
    }

    /// <summary>Creates a fresh Octokit client credentialed with the given token, scoped to a single call.</summary>
    /// <param name="token">The Personal Access Token to authenticate with. Never logged.</param>
    /// <returns>A new <see cref="GitHubClient"/> instance.</returns>
    private static GitHubClient CreateClient(string token) =>
        new(new ProductHeaderValue("DashboardApp"))
        {
            Credentials = new Credentials(token)
        };
}
