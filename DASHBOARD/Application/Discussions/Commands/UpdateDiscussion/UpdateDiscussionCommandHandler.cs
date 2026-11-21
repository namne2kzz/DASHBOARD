using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Helpers;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DASHBOARD.Application.Discussions.Commands.UpdateDiscussion;

/// <summary>Handles <see cref="UpdateDiscussionCommand"/>: enforces author-only edit rule then updates the body.</summary>
public sealed class UpdateDiscussionCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow,
    IStorageService       storage,
    IAppSettings          settings,
    ILogger<UpdateDiscussionCommandHandler> logger) : IRequestHandler<UpdateDiscussionCommand, Result>
{
    /// <summary>Validates the caller is the original author, applies the body change, drops images the edit removed, and commits.</summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(UpdateDiscussionCommand command, CancellationToken ct)
    {
        var entry = await db.Set<DiscussionEntry>()
            .AsTracking()
            .FirstOrDefaultAsync(d => d.Id == command.EntryId
                && d.SprintTaskId == command.SprintTaskId
                && d.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(DiscussionEntry), command.EntryId);

        if (!user.IsSelf(entry.AuthorId))
            throw new ForbiddenException("Only the original author may edit a comment.");

        var orphaned = CommentImageHelper.FindRemoved(
            entry.Body, command.Body, settings.MinioPublicBaseUrl, settings.MinioAttachmentBucket);

        entry.Body = command.Body;
        entry.Touch();

        await uow.CommitAsync(ct);

        // After the commit: the edit is what the user asked for, and it must stand even if the
        // object store is unreachable. A leftover object costs space; a failed edit costs work.
        await DeleteImagesAsync(orphaned, ct);

        return Result.Ok;
    }

    /// <summary>Removes images an edit orphaned, logging rather than throwing when the store refuses.</summary>
    /// <param name="objectKeys">Keys no longer referenced by the comment.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task DeleteImagesAsync(IReadOnlyCollection<string> objectKeys, CancellationToken ct)
    {
        foreach (var key in objectKeys)
        {
            try
            {
                await storage.DeleteAsync(settings.MinioAttachmentBucket, key, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete orphaned comment image {ObjectKey}.", key);
            }
        }
    }
}
