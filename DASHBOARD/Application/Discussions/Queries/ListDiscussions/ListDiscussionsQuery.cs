using DASHBOARD.Application.Discussions.DTOs;
using MediatR;

namespace DASHBOARD.Application.Discussions.Queries.ListDiscussions;

/// <summary>
/// Returns the discussion thread for a sprint task: every root comment oldest-first, each with only
/// its first reply preloaded.
/// </summary>
/// <remarks>
/// The rest of a root's replies are fetched on demand through <c>ListRepliesQuery</c>. Loading every
/// reply up front meant a task with a long argument in one thread paid for all of it on open.
/// </remarks>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The sprint task whose discussions to fetch.</param>
public sealed record ListDiscussionsQuery(Guid RepositoryId, Guid SprintTaskId) : IRequest<IReadOnlyList<DiscussionDto>>;
