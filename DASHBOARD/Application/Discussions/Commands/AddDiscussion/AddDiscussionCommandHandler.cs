using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Discussions.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Discussions.Commands.AddDiscussion;

/// <summary>Handles <see cref="AddDiscussionCommand"/>: verifies membership and sprint task existence, then creates the comment.</summary>
public sealed class AddDiscussionCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<AddDiscussionCommand, DiscussionDto>
{
    /// <summary>Validates membership, creates the discussion entry, and returns the DTO.</summary>
    /// <param name="command">The add command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="DiscussionDto"/>.</returns>
    public async Task<DiscussionDto> Handle(AddDiscussionCommand command, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(command.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        if (!await db.Set<SprintTask>().AnyAsync(t => t.Id == command.SprintTaskId && t.RepositoryId == command.RepositoryId, ct))
            throw new NotFoundException(nameof(SprintTask), command.SprintTaskId);

        var author = await db.Set<User>().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.UserId, ct)!;

        var entry = new DiscussionEntry
        {
            SprintTaskId = command.SprintTaskId,
            RepositoryId = command.RepositoryId,
            AuthorId     = user.UserId,
            Body         = command.Body,
        };
        db.Set<DiscussionEntry>().Add(entry);
        await uow.CommitAsync(ct);

        return new DiscussionDto(entry.Id, entry.SprintTaskId, user.UserId,
            author!.Name, author.AvatarClass, entry.Body, entry.CreatedAt, null);
    }
}
