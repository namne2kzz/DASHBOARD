using DASHBOARD.Application.Attachments.DTOs;
using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Attachments.Commands.AddAttachment;

/// <summary>Confirms an uploaded file and records it as a work-item attachment.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The work item the file belongs to.</param>
/// <param name="FileName">Original file name.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="SizeBytes">File size in bytes.</param>
/// <param name="ObjectKey">The storage object key returned by the upload-url request.</param>
public sealed record AddAttachmentCommand(
    Guid   RepositoryId,
    Guid   SprintTaskId,
    string FileName,
    string ContentType,
    long   SizeBytes,
    string ObjectKey) : IRequest<Result<AttachmentDto>>;
