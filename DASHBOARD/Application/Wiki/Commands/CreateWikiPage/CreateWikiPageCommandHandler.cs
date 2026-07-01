using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Wiki.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Wiki.Commands.CreateWikiPage;

/// <summary>Handles <see cref="CreateWikiPageCommand"/>: checks ManageWiki permission, validates parent existence, and creates the page.</summary>
public sealed class CreateWikiPageCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<CreateWikiPageCommand, WikiPageDto>
{
    /// <summary>Validates permission and parent, creates the wiki page, and returns the DTO.</summary>
    /// <param name="command">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="WikiPageDto"/>.</returns>
    public async Task<WikiPageDto> Handle(CreateWikiPageCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageWiki, ct))
            throw new UnauthorizedAccessException("You do not have permission to manage wiki in this repository.");

        if (command.ParentId.HasValue &&
            !await db.Set<WikiPage>().AnyAsync(p => p.Id == command.ParentId && p.RepositoryId == command.RepositoryId, ct))
            throw new NotFoundException(nameof(WikiPage), command.ParentId.Value);

        var now  = DateTime.UtcNow;
        var page = new WikiPage
        {
            RepositoryId = command.RepositoryId,
            Title        = command.Title,
            Content      = command.Content,
            LastUpdated  = now,
            ParentId     = command.ParentId,
        };
        db.Set<WikiPage>().Add(page);
        await uow.CommitAsync(ct);

        return new WikiPageDto(page.Id, page.RepositoryId, page.Title, page.Content, page.LastUpdated, page.ParentId, page.CreatedAt, []);
    }
}
