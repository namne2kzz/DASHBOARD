using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Wiki.Commands.UpdateWikiPage;

/// <summary>Handles <see cref="UpdateWikiPageCommand"/>: checks ManageWiki permission, updates the page, and stamps LastUpdated.</summary>
public sealed class UpdateWikiPageCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateWikiPageCommand, Result>
{
    /// <summary>Validates permission, updates title and content, stamps LastUpdated, and commits.</summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(UpdateWikiPageCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageWiki, ct))
            return Result.Failure("You do not have permission to manage wiki in this repository.");

        var page = await db.Set<WikiPage>()
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == command.PageId && p.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(WikiPage), command.PageId);

        page.Title       = command.Title;
        page.Content     = command.Content;
        page.LastUpdated = DateTime.UtcNow;
        page.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
