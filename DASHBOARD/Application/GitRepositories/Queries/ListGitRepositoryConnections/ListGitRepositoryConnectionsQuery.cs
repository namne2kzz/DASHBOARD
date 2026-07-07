using DASHBOARD.Application.GitRepositories.DTOs;
using MediatR;

namespace DASHBOARD.Application.GitRepositories.Queries.ListGitRepositoryConnections;

/// <summary>Returns the configured GitHub connections for a project (credentials stripped). The caller must be a project member.</summary>
/// <param name="RepositoryId">The project (<c>Repository</c>) to list connections for.</param>
public sealed record ListGitRepositoryConnectionsQuery(Guid RepositoryId) : IRequest<IReadOnlyList<GitRepositoryConnectionSummaryDto>>;
