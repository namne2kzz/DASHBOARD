using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Discussions.Commands.DeleteDiscussion;

/// <summary>Handles <see cref="DeleteDiscussionCommand"/>: allows author or global admin to soft-delete the comment.</summary>
public sealed class DeleteDiscussionCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<DeleteDiscussionCommand, Result>
{
    /// <summary>Validates author-or-admin rule, stamps soft-delete metadata, and commits.</summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> on permission denial.</returns>
    public async Task<Result> Handle(DeleteDiscussionCommand command, CancellationToken ct)
    {
        var entry = await db.Set<DiscussionEntry>()
            .FirstOrDefaultAsync(d => d.Id == command.EntryId
                && d.SprintTaskId == command.SprintTaskId
                && d.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(DiscussionEntry), command.EntryId);

        if (!user.IsSelf(entry.AuthorId) && !await user.IsGlobalAdminAsync(ct))
            return Result.Failure("Only the original author or a global admin may delete a comment.");

        entry.DeletedByUserId = user.UserId;
        db.Set<DiscussionEntry>().Remove(entry);

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
