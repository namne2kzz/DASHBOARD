using System.Text.Json;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.GitRepositories.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using DASHBOARD.Infrastructure.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Application.GitRepositories.Queries.GetGitRepositoryOverview;

/// <summary>
/// Handles <see cref="GetGitRepositoryOverviewQuery"/>: checks membership, resolves which configured
/// connection to show, and returns its live GitHub overview through a short-TTL read-through cache.
/// </summary>
/// <remarks>
/// Per plan §3.2, this query always returns 200 for a GitHub-side failure — only membership/not-found
/// failures (project doesn't exist, caller isn't a member) still throw. A found-but-unreachable connection
/// is reported via <c>Status</c>/<c>LastSyncError</c> on the DTO instead of propagating a Git*Exception.
/// </remarks>
public sealed class GetGitRepositoryOverviewQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext user,
    IOptionsMonitor<GitConnectionsOptions> gitConnections,
    IGitHubIntegrationService gitHub,
    IDistributedCache cache)
    : IRequestHandler<GetGitRepositoryOverviewQuery, GitRepositoryOverviewDto>
{
    /// <summary>How long a fetched overview is cached before the next request re-pulls from GitHub.</summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    /// <summary>How many recent commits to pull per overview.</summary>
    private const int RecentCommitCount = 30;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Validates membership, resolves the project's <c>Code</c>, and builds the overview for the selected (or default) connection.</summary>
    /// <param name="query">The query containing the project (repository) ID and optional connection selector.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="GitRepositoryOverviewDto"/> with <c>HasConnection = false</c> when the project has no configured connections.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller is not a member of the project.</exception>
    /// <exception cref="NotFoundException">Thrown when the project does not exist or is archived.</exception>
    public async Task<GitRepositoryOverviewDto> Handle(GetGitRepositoryOverviewQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var repo = await db.Set<Repository>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == query.RepositoryId && !r.IsArchived, ct)
            ?? throw new NotFoundException(nameof(Repository), query.RepositoryId);

        if (!gitConnections.CurrentValue.TryGetValue(repo.Code, out var entries) || entries.Count == 0)
        {
            return new GitRepositoryOverviewDto(
                HasConnection: false,
                RepoUrl: null,
                FullName: null,
                DefaultBranch: null,
                Branches: [],
                Commits: [],
                PullRequests: [],
                RateLimit: null,
                Status: null,
                LastSyncError: null);
        }

        // Resolve which entry to show: explicit RepoUrl match, else the primary entry,
        // else the first entry (documented fallback — see GitConnectionEntry.IsPrimary).
        var selected = (query.RepoUrl is not null
                ? entries.FirstOrDefault(e => string.Equals(e.RepoUrl, query.RepoUrl, StringComparison.OrdinalIgnoreCase))
                : null)
            ?? entries.FirstOrDefault(e => e.IsPrimary)
            ?? entries[0];

        var (owner, name) = GitRepoUrlParser.Parse(selected.RepoUrl);
        var fullName = $"{owner}/{name}";

        var cacheKey = $"git:overview:{query.RepositoryId}:{selected.RepoUrl}";

        var cached = await cache.GetAsync(cacheKey, ct);
        if (cached is not null)
        {
            var cachedDto = JsonSerializer.Deserialize<GitRepositoryOverviewDto>(cached, JsonOptions);
            if (cachedDto is not null)
                return cachedDto;
        }

        GitRepositoryOverviewDto overview;

        try
        {
            var branchesTask = gitHub.ListBranchesAsync(owner, name, selected.Token, ct);
            var commitsTask = gitHub.ListRecentCommitsAsync(owner, name, selected.Token, selected.DefaultBranchOverride, RecentCommitCount, ct);
            var pullRequestsTask = gitHub.ListPullRequestsAsync(owner, name, selected.Token, ct);
            var rateLimitTask = gitHub.GetRateLimitAsync(selected.Token, ct);

            await Task.WhenAll(branchesTask, commitsTask, pullRequestsTask, rateLimitTask);

            var branches = branchesTask.Result;
            var defaultBranch = selected.DefaultBranchOverride ?? branches.FirstOrDefault(b => b.IsDefault)?.Name;

            // ListRecentCommitsAsync leaves Branch empty when no explicit branch was requested (it used
            // GitHub's own default-branch resolution) — backfill it here now that defaultBranch is known,
            // so the frontend always has a branch label to show against each commit.
            var commits = commitsTask.Result
                .Select(c => c with
                {
                    Branch = string.IsNullOrEmpty(c.Branch) ? defaultBranch ?? string.Empty : c.Branch,
                    LinkedWorkItemId = GitWorkItemLinkResolver.Resolve(c.Message, repo.Code)
                })
                .ToList();
            var pullRequests = pullRequestsTask.Result
                .Select(p => p with { LinkedWorkItemId = GitWorkItemLinkResolver.Resolve(p.Title, repo.Code) })
                .ToList();

            overview = new GitRepositoryOverviewDto(
                HasConnection: true,
                RepoUrl: selected.RepoUrl,
                FullName: fullName,
                DefaultBranch: defaultBranch,
                Branches: branches,
                Commits: commits,
                PullRequests: pullRequests,
                RateLimit: rateLimitTask.Result,
                Status: "Connected",
                LastSyncError: null);

            await cache.SetAsync(
                cacheKey,
                JsonSerializer.SerializeToUtf8Bytes(overview, JsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
                ct);
        }
        catch (GitRateLimitExceededException ex)
        {
            overview = BuildFailureOverview(selected.RepoUrl, fullName, "RateLimited", ex.Message);
        }
        catch (GitCredentialInvalidException ex)
        {
            overview = BuildFailureOverview(selected.RepoUrl, fullName, "Invalid", ex.Message);
        }
        catch (GitProviderUnavailableException ex)
        {
            overview = BuildFailureOverview(selected.RepoUrl, fullName, "Unavailable", ex.Message);
        }

        return overview;
    }

    /// <summary>Builds the DTO returned when a configured connection exists but the live GitHub call failed.</summary>
    /// <param name="repoUrl">The configured connection's URL.</param>
    /// <param name="fullName">The <c>owner/repo</c> full name parsed from the URL.</param>
    /// <param name="status">A short status code describing the failure (e.g. <c>"Invalid"</c>, <c>"RateLimited"</c>, <c>"Unavailable"</c>).</param>
    /// <param name="lastSyncError">The failure message surfaced to the frontend's error banner.</param>
    /// <returns>A <see cref="GitRepositoryOverviewDto"/> with <c>HasConnection = true</c> and empty collections.</returns>
    private static GitRepositoryOverviewDto BuildFailureOverview(string repoUrl, string fullName, string status, string lastSyncError) =>
        new(
            HasConnection: true,
            RepoUrl: repoUrl,
            FullName: fullName,
            DefaultBranch: null,
            Branches: [],
            Commits: [],
            PullRequests: [],
            RateLimit: null,
            Status: status,
            LastSyncError: lastSyncError);
}
