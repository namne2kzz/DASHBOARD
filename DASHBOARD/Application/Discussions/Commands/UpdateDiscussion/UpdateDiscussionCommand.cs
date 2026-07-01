using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Discussions.Commands.UpdateDiscussion;

/// <summary>Edits the body of an existing discussion comment. Only the original author may edit.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The parent sprint task.</param>
/// <param name="EntryId">The comment to edit.</param>
/// <param name="Body">The new comment body.</param>
public sealed record UpdateDiscussionCommand(
    Guid   RepositoryId,
    Guid   SprintTaskId,
    Guid   EntryId,
    string Body) : IRequest<Result>;
