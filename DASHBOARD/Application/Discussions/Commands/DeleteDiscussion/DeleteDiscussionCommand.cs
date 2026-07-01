using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Discussions.Commands.DeleteDiscussion;

/// <summary>Soft-deletes a discussion comment. Only the original author or a global admin may delete.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The parent sprint task.</param>
/// <param name="EntryId">The comment to delete.</param>
public sealed record DeleteDiscussionCommand(
    Guid RepositoryId,
    Guid SprintTaskId,
    Guid EntryId) : IRequest<Result>;
