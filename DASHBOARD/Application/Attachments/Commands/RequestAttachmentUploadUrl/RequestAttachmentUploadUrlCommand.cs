using DASHBOARD.Application.Attachments.DTOs;
using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Attachments.Commands.RequestAttachmentUploadUrl;

/// <summary>
/// Requests a presigned upload URL for a work-item attachment or a pasted inline image.
/// The server assigns the object key; the browser then PUTs the file directly to object storage.
/// </summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The work item the file belongs to.</param>
/// <param name="FileName">Original file name (used only to derive an extension for the key).</param>
/// <param name="ContentType">MIME type — validated against the allowlist.</param>
/// <param name="SizeBytes">File size in bytes — validated against the size limit.</param>
/// <param name="InlineImage">True for a pasted inline image (stricter image allowlist); false for a general attachment.</param>
public sealed record RequestAttachmentUploadUrlCommand(
    Guid   RepositoryId,
    Guid   SprintTaskId,
    string FileName,
    string ContentType,
    long   SizeBytes,
    bool   InlineImage) : IRequest<Result<AttachmentUploadUrlResult>>;
