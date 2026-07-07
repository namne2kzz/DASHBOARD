using DASHBOARD.Application.GitRepositories.DTOs;
using MediatR;

namespace DASHBOARD.Application.GitRepositories.Queries.GetGitRepositoryOverview;

/// <summary>Returns the GitHub overview for a project's connected repository. The caller must be a project member.</summary>
/// <param name="RepositoryId">The project (<c>Repository</c>) to load the overview for.</param>
/// <param name="RepoUrl">
/// Optional connection to select when the project has multiple configured repos. When <c>null</c>,
/// the entry marked <c>IsPrimary</c> is used, falling back to the first configured entry.
/// </param>
public sealed record GetGitRepositoryOverviewQuery(Guid RepositoryId, string? RepoUrl) : IRequest<GitRepositoryOverviewDto>;
