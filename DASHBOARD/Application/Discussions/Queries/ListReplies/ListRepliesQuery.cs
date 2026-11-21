using DASHBOARD.Application.Discussions.DTOs;
using MediatR;

namespace DASHBOARD.Application.Discussions.Queries.ListReplies;

/// <summary>Returns a page of replies to one root comment, oldest-first.</summary>
/// <remarks>
/// Backs the thread's "show more replies" control, so a long argument under a single comment is
/// paid for only when the reader asks to see it.
/// </remarks>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The parent sprint task.</param>
/// <param name="ParentId">The root comment whose replies to fetch.</param>
/// <param name="Skip">How many replies to skip — the count already on screen.</param>
/// <param name="Take">How many replies to return.</param>
public sealed record ListRepliesQuery(
    Guid RepositoryId,
    Guid SprintTaskId,
    Guid ParentId,
    int  Skip,
    int  Take) : IRequest<IReadOnlyList<DiscussionDto>>;
