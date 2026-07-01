using DASHBOARD.Application.Discussions.DTOs;
using MediatR;

namespace DASHBOARD.Application.Discussions.Queries.ListDiscussions;

/// <summary>Returns all non-deleted discussion entries for a sprint task ordered by creation time.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The sprint task whose discussions to fetch.</param>
public sealed record ListDiscussionsQuery(Guid RepositoryId, Guid SprintTaskId) : IRequest<IReadOnlyList<DiscussionDto>>;
