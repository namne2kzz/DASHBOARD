using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Discussions.Commands.UpdateDiscussion;

/// <summary>Handles <see cref="UpdateDiscussionCommand"/>: enforces author-only edit rule then updates the body.</summary>
public sealed class UpdateDiscussionCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateDiscussionCommand, Result>
{
    /// <summary>Validates the caller is the original author, applies the body change, and commits.</summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> if not the author.</returns>
    public async Task<Result> Handle(UpdateDiscussionCommand command, CancellationToken ct)
    {
        var entry = await db.Set<DiscussionEntry>()
            .AsTracking()
            .FirstOrDefaultAsync(d => d.Id == command.EntryId
                && d.SprintTaskId == command.SprintTaskId
                && d.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(DiscussionEntry), command.EntryId);

        if (!user.IsSelf(entry.AuthorId))
            return Result.Failure("Only the original author may edit a comment.");

        entry.Body = command.Body;
        entry.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
