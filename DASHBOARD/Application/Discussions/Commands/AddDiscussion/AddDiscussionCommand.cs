using DASHBOARD.Application.Discussions.DTOs;
using MediatR;

namespace DASHBOARD.Application.Discussions.Commands.AddDiscussion;

/// <summary>Posts a new comment on a sprint task's discussion thread.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The sprint task to comment on.</param>
/// <param name="Body">The Markdown comment body.</param>
public sealed record AddDiscussionCommand(
    Guid   RepositoryId,
    Guid   SprintTaskId,
    string Body) : IRequest<DiscussionDto>;
