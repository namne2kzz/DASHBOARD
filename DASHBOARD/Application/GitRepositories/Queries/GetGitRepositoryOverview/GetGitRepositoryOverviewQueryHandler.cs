using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.GitRepositories.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using DASHBOARD.Infrastructure.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Application.GitRepositories.Queries.GetGitRepositoryOverview;

/// <summary>
/// Handles <see cref="GetGitRepositoryOverviewQuery"/>: checks membership, resolves which configured
/// connection to show, and returns its overview. Phase 1 makes no live GitHub call yet — a found
/// connection is reported as <c>Status = "PendingValidation"</c> with empty branch/commit/PR collections,
/// ready to be populated by <c>IGitHubIntegrationService</c> in Phase 2.
/// </summary>
public sealed class GetGitRepositoryOverviewQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext user,
    IOptionsMonitor<GitConnectionsOptions> gitConnections)
    : IRequestHandler<GetGitRepositoryOverviewQuery, GitRepositoryOverviewDto>
{
    /// <summary>Validates membership, resolves the project's <c>Code</c>, and builds the overview for the selected (or default) connection.</summary>
    /// <param name="query">The query containing the project (repository) ID and optional connection selector.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="GitRepositoryOverviewDto"/> with <c>HasConnection = false</c> when the project has no configured connections.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller is not a member of the project.</exception>
    /// <exception cref="NotFoundException">Thrown when the project does not exist or is archived.</exception>
    public async Task<GitRepositoryOverviewDto> Handle(GetGitRepositoryOverviewQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

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

        return new GitRepositoryOverviewDto(
            HasConnection: true,
            RepoUrl: selected.RepoUrl,
            FullName: $"{owner}/{name}",
            DefaultBranch: selected.DefaultBranchOverride,
            Branches: [],
            Commits: [],
            PullRequests: [],
            RateLimit: null,
            Status: "PendingValidation",
            LastSyncError: null);
    }
}
