using DASHBOARD.Application.Attachments.DTOs;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Helpers;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Attachments.Commands.RequestAttachmentUploadUrl;

/// <summary>
/// Handles <see cref="RequestAttachmentUploadUrlCommand"/>: validates membership, file type and size,
/// assigns a server-controlled object key, and returns a presigned PUT URL plus the eventual public URL.
/// </summary>
public sealed class RequestAttachmentUploadUrlCommandHandler(
    IApplicationDbContext db,
    IStorageService       storage,
    IAppSettings          settings,
    IRequestUserContext   user) : IRequestHandler<RequestAttachmentUploadUrlCommand, Result<AttachmentUploadUrlResult>>
{
    private static readonly TimeSpan UploadUrlTtl = TimeSpan.FromMinutes(10);

    /// <summary>Validates the request, builds the object key, and returns the presigned upload target.</summary>
    /// <param name="command">The upload-url request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Presigned URL, object key and public URL on success; failure on a rejected file.</returns>
    public async Task<Result<AttachmentUploadUrlResult>> Handle(
        RequestAttachmentUploadUrlCommand command, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(command.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        if (!await db.Set<SprintTask>().AnyAsync(
                t => t.Id == command.SprintTaskId && t.RepositoryId == command.RepositoryId && !t.IsDeleted, ct))
            throw new NotFoundException(nameof(SprintTask), command.SprintTaskId);

        var valid = command.InlineImage
            ? AttachmentPolicy.IsValidImage(command.ContentType, command.SizeBytes)
            : AttachmentPolicy.IsValidAttachment(command.ContentType, command.SizeBytes);

        if (!valid)
            return Result<AttachmentUploadUrlResult>.Failure(
                command.InlineImage
                    ? "Only PNG, JPEG, GIF or WebP images up to 10 MB can be pasted."
                    : "File type is not allowed or exceeds the 50 MB limit.");

        var ext       = AttachmentPolicy.ExtensionFor(command.ContentType);
        var objectKey = $"workitems/{command.RepositoryId}/{command.SprintTaskId}/{Guid.NewGuid():N}{ext}";
        var bucket    = settings.MinioAttachmentBucket;

        var uploadUrl = await storage.GenerateUploadUrlAsync(bucket, objectKey, UploadUrlTtl, ct);
        var publicUrl = storage.GetPublicUrl(bucket, objectKey);

        return Result<AttachmentUploadUrlResult>.Success(
            new AttachmentUploadUrlResult(uploadUrl, objectKey, publicUrl));
    }
}
