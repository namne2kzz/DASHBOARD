using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Attachments.Commands.DeleteAttachment;

/// <summary>Removes a work-item attachment (DB row + stored object).</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="AttachmentId">The attachment to delete.</param>
public sealed record DeleteAttachmentCommand(
    Guid RepositoryId,
    Guid AttachmentId) : IRequest<Result>;
