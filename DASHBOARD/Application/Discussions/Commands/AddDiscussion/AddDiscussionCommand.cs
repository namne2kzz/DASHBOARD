using DASHBOARD.Application.Discussions.DTOs;
using MediatR;

namespace DASHBOARD.Application.Discussions.Commands.AddDiscussion;

/// <summary>Posts a new comment on a sprint task's discussion thread.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The sprint task to comment on.</param>
/// <param name="Body">The Markdown comment body.</param>
/// <param name="ParentId">
/// The comment being replied to, or null for a new root comment. If it names a reply, the new entry
/// is re-parented to that reply's own root — the thread never goes deeper than one level.
/// </param>
public sealed record AddDiscussionCommand(
    Guid   RepositoryId,
    Guid   SprintTaskId,
    string Body,
    Guid?  ParentId = null) : IRequest<DiscussionDto>;
