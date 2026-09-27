using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.GitRepositories.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using DASHBOARD.Infrastructure.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DASHBOARD.Application.GitRepositories.Queries.ListGitRepositoryConnections;

/// <summary>Handles <see cref="ListGitRepositoryConnectionsQuery"/>: checks membership then reads the project's configured GitHub connections (never the token).</summary>
public sealed class ListGitRepositoryConnectionsQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext user,
    IOptionsMonitor<GitConnectionsOptions> gitConnections)
    : IRequestHandler<ListGitRepositoryConnectionsQuery, IReadOnlyList<GitRepositoryConnectionSummaryDto>>
{
    /// <summary>Validates membership, resolves the project's <c>Code</c>, and maps any configured connections to non-secret DTOs.</summary>
    /// <param name="query">The query containing the project (repository) ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The configured connections, or an empty list when the project's code has no <c>GitConnections</c> entry.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller is not a member of the project.</exception>
    /// <exception cref="NotFoundException">Thrown when the project does not exist or is archived.</exception>
    public async Task<IReadOnlyList<GitRepositoryConnectionSummaryDto>> Handle(
        ListGitRepositoryConnectionsQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var repo = await db.Set<Repository>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == query.RepositoryId && !r.IsArchived, ct)
            ?? throw new NotFoundException(nameof(Repository), query.RepositoryId);

        if (!gitConnections.CurrentValue.TryGetValue(repo.Code, out var entries) || entries.Count == 0)
            return [];

        return entries
            .Select(entry =>
            {
                var (owner, name) = GitRepoUrlParser.Parse(entry.RepoUrl);
                return new GitRepositoryConnectionSummaryDto(entry.RepoUrl, owner, name, entry.IsPrimary);
            })
            .ToList();
    }
}
