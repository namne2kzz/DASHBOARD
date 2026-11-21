using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Helpers;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DASHBOARD.Application.Discussions.Commands.DeleteDiscussion;

/// <summary>Handles <see cref="DeleteDiscussionCommand"/>: allows author or global admin to soft-delete the comment.</summary>
public sealed class DeleteDiscussionCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow,
    IStorageService       storage,
    IAppSettings          settings,
    ILogger<DeleteDiscussionCommandHandler> logger) : IRequestHandler<DeleteDiscussionCommand, Result>
{
    /// <summary>Validates author-or-admin rule, removes the comment with any replies, drops their images, and commits.</summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(DeleteDiscussionCommand command, CancellationToken ct)
    {
        var entry = await db.Set<DiscussionEntry>()
            .FirstOrDefaultAsync(d => d.Id == command.EntryId
                && d.SprintTaskId == command.SprintTaskId
                && d.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(DiscussionEntry), command.EntryId);

        if (!user.IsSelf(entry.AuthorId) && !await user.IsGlobalAdminAsync(ct))
            throw new ForbiddenException("Only the original author or a global admin may delete a comment.");

        // Replies are removed with their root: the FK is Restrict (SQL Server rejects a
        // self-referencing cascade), so without this the delete would fail on any answered comment.
        var replies = entry.ParentId is null
            ? await db.Set<DiscussionEntry>().Where(d => d.ParentId == entry.Id).ToListAsync(ct)
            : [];

        var orphaned = replies.Append(entry)
            .SelectMany(d => CommentImageHelper.ExtractObjectKeys(
                d.Body, settings.MinioPublicBaseUrl, settings.MinioAttachmentBucket))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var reply in replies)
        {
            reply.DeletedByUserId = user.UserId;
            db.Set<DiscussionEntry>().Remove(reply);
        }

        entry.DeletedByUserId = user.UserId;
        db.Set<DiscussionEntry>().Remove(entry);

        await uow.CommitAsync(ct);

        // After the commit, for the same reason as an edit: losing the object store must not
        // leave the comment undeleted.
        foreach (var key in orphaned)
        {
            try
            {
                await storage.DeleteAsync(settings.MinioAttachmentBucket, key, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete image {ObjectKey} of deleted comment.", key);
            }
        }

        return Result.Ok;
    }
}
