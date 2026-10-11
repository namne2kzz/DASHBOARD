using DASHBOARD.Application.Discussions.DTOs;
using MediatR;

namespace DASHBOARD.Application.Discussions.Queries.ListMentionCandidates;

/// <summary>
/// Returns the users who can be @mentioned in a sprint task's discussion:
/// the capacity members of the task's sprint, or — when the task is not in a sprint — the repository's members.
/// </summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The sprint task being commented on.</param>
public sealed record ListMentionCandidatesQuery(
    Guid RepositoryId,
    Guid SprintTaskId) : IRequest<IReadOnlyList<MentionCandidateDto>>;
