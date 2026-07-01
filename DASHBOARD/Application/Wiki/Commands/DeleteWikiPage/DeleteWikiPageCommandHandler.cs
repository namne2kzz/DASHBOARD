using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Wiki.Commands.DeleteWikiPage;

/// <summary>Handles <see cref="DeleteWikiPageCommand"/>: blocks deletion when children exist, then soft-deletes the page.</summary>
public sealed class DeleteWikiPageCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<DeleteWikiPageCommand, Result>
{
    /// <summary>Validates permission, checks for children, and soft-deletes the page.</summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> when children block deletion.</returns>
    public async Task<Result> Handle(DeleteWikiPageCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageWiki, ct))
            return Result.Failure("You do not have permission to manage wiki in this repository.");

        var page = await db.Set<WikiPage>()
            .FirstOrDefaultAsync(p => p.Id == command.PageId && p.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(WikiPage), command.PageId);

        var hasChildren = await db.Set<WikiPage>()
            .AnyAsync(p => p.ParentId == command.PageId, ct);
        if (hasChildren)
            return Result.Failure("Cannot delete a wiki page that has children. Delete children first.");

        page.DeletedByUserId = user.UserId;
        db.Set<WikiPage>().Remove(page);

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
