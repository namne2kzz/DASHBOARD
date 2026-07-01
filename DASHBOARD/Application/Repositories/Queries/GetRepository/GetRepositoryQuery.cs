using DASHBOARD.Application.Repositories.DTOs;
using MediatR;

namespace DASHBOARD.Application.Repositories.Queries.GetRepository;

/// <summary>Returns a single repository by ID. The caller must be a member (or global admin).</summary>
/// <param name="RepositoryId">The repository to fetch.</param>
public sealed record GetRepositoryQuery(Guid RepositoryId) : IRequest<RepositoryDto>;
