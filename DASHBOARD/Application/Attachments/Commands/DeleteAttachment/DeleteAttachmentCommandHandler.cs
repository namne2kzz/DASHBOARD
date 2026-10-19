using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Attachments.Commands.DeleteAttachment;

/// <summary>
/// Handles <see cref="DeleteAttachmentCommand"/>: allows the uploader or a board manager to remove
/// an attachment, deletes the stored object, and writes a history entry.
/// </summary>
public sealed class DeleteAttachmentCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IStorageService       storage,
    IAppSettings          settings,
    IHistoryService       history,
    IUnitOfWork           uow) : IRequestHandler<DeleteAttachmentCommand, Result>
{
    /// <summary>Validates permission, removes the row and object, and audits the action.</summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(DeleteAttachmentCommand command, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(command.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var attachment = await db.Set<WorkItemAttachment>()
            .FirstOrDefaultAsync(a => a.Id == command.AttachmentId && a.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(WorkItemAttachment), command.AttachmentId);

        // The uploader can always remove their own file; otherwise ManageBoard is required.
        if (attachment.UploadedById != user.UserId &&
            !await user.CanAsync(command.RepositoryId, SystemFunction.ManageBoard, ct))
            throw new ForbiddenException("You do not have permission to delete this attachment.");

        db.Set<WorkItemAttachment>().Remove(attachment);

        history.Record(attachment.SprintTaskId, command.RepositoryId, user.UserId,
            $"Removed file '{attachment.FileName}'.");

        await uow.CommitAsync(ct);

        // Best-effort object cleanup after the row is gone; a storage hiccup must not fail the command.
        try
        {
            await storage.DeleteAsync(settings.MinioAttachmentBucket, attachment.ObjectKey, ct);
        }
        catch
        {
            // Orphaned object is tolerable; the row (source of truth) is already removed.
        }

        return Result.Ok;
    }
}
