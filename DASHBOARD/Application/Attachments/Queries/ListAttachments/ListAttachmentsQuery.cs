using DASHBOARD.Application.Attachments.DTOs;
using MediatR;

namespace DASHBOARD.Application.Attachments.Queries.ListAttachments;

/// <summary>Lists the attachments of a work item, newest first.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The work item.</param>
public sealed record ListAttachmentsQuery(
    Guid RepositoryId,
    Guid SprintTaskId) : IRequest<IReadOnlyList<AttachmentDto>>;
