using DASHBOARD.Application.Sprints.DTOs;
using MediatR;

namespace DASHBOARD.Application.Sprints.Queries.ListSprints;

/// <summary>Returns all sprints for a repository ordered by start date descending.</summary>
/// <param name="RepositoryId">The repository to query.</param>
public sealed record ListSprintsQuery(Guid RepositoryId) : IRequest<IReadOnlyList<SprintDto>>;
