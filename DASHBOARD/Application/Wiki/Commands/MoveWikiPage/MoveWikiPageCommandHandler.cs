using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Wiki.Commands.MoveWikiPage;

/// <summary>Handles <see cref="MoveWikiPageCommand"/>: validates that the new parent is not a descendant of the page (circular reference guard), then updates the hierarchy.</summary>
public sealed class MoveWikiPageCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<MoveWikiPageCommand, Result>
{
    /// <summary>Validates permission, checks for circular references, and updates the ParentId.</summary>
    /// <param name="command">The move command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> on circular reference or permission denial.</returns>
    public async Task<Result> Handle(MoveWikiPageCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageWiki, ct))
            return Result.Failure("You do not have permission to manage wiki in this repository.");

        var page = await db.Set<WikiPage>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == command.PageId && p.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(WikiPage), command.PageId);

        // Prevent circular reference: new parent must not be a descendant of this page.
        if (command.NewParentId.HasValue)
        {
            if (command.NewParentId == command.PageId)
                return Result.Failure("A page cannot be its own parent.");

            var allPages = await db.Set<WikiPage>().AsNoTracking()
                .Where(p => p.RepositoryId == command.RepositoryId)
                .ToListAsync(ct);

            if (IsDescendant(allPages, command.PageId, command.NewParentId.Value))
                return Result.Failure("Cannot move a page to one of its own descendants (circular reference).");
        }

        page.ParentId = command.NewParentId;
        page.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }

    private static bool IsDescendant(List<WikiPage> all, Guid ancestorId, Guid targetId)
    {
        var current = all.FirstOrDefault(p => p.Id == targetId);
        while (current is not null)
        {
            if (current.ParentId == ancestorId) return true;
            current = current.ParentId.HasValue ? all.FirstOrDefault(p => p.Id == current.ParentId) : null;
        }
        return false;
    }
}
