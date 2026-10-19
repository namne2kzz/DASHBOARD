using DASHBOARD.Application.Attachments.DTOs;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Helpers;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Attachments.Commands.AddAttachment;

/// <summary>
/// Handles <see cref="AddAttachmentCommand"/>: validates membership, file policy and object existence,
/// records the attachment row, writes a history entry, and returns the attachment DTO.
/// </summary>
public sealed class AddAttachmentCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IStorageService       storage,
    IAppSettings          settings,
    IHistoryService       history,
    IUnitOfWork           uow) : IRequestHandler<AddAttachmentCommand, Result<AttachmentDto>>
{
    /// <summary>Confirms the uploaded object, persists the attachment, and audits the action.</summary>
    /// <param name="command">The add-attachment command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="AttachmentDto"/>; failure on a rejected file.</returns>
    public async Task<Result<AttachmentDto>> Handle(AddAttachmentCommand command, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(command.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        if (!await db.Set<SprintTask>().AnyAsync(
                t => t.Id == command.SprintTaskId && t.RepositoryId == command.RepositoryId && !t.IsDeleted, ct))
            throw new NotFoundException(nameof(SprintTask), command.SprintTaskId);

        if (!AttachmentPolicy.IsValidAttachment(command.ContentType, command.SizeBytes))
            return Result<AttachmentDto>.Failure("File type is not allowed or exceeds the 50 MB limit.");

        // Guard the object key against path traversal / cross-task reuse.
        var expectedPrefix = $"workitems/{command.RepositoryId}/{command.SprintTaskId}/";
        if (!command.ObjectKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
            return Result<AttachmentDto>.Failure("Invalid object key.");

        var bucket = settings.MinioAttachmentBucket;
        try
        {
            await storage.HeadObjectAsync(bucket, command.ObjectKey, ct);
        }
        catch (StorageObjectNotFoundException)
        {
            return Result<AttachmentDto>.Failure("Upload not found. Please try uploading again.");
        }

        var author = await db.Set<User>().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.UserId, ct);

        var attachment = new WorkItemAttachment
        {
            SprintTaskId = command.SprintTaskId,
            RepositoryId = command.RepositoryId,
            UploadedById = user.UserId,
            FileName     = command.FileName,
            ContentType  = command.ContentType,
            SizeBytes    = command.SizeBytes,
            ObjectKey    = command.ObjectKey,
            PublicUrl    = storage.GetPublicUrl(bucket, command.ObjectKey),
        };
        db.Set<WorkItemAttachment>().Add(attachment);

        history.Record(command.SprintTaskId, command.RepositoryId, user.UserId,
            $"Attached file '{command.FileName}'.");

        await uow.CommitAsync(ct);

        return Result<AttachmentDto>.Success(new AttachmentDto(
            attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes,
            attachment.PublicUrl, author?.Name ?? "Unknown", author?.AvatarClass, attachment.CreatedAt));
    }
}
