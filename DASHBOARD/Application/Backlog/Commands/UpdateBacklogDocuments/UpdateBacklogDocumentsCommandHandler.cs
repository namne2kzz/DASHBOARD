using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogDocuments;

/// <summary>Handles <see cref="UpdateBacklogDocumentsCommand"/>: replaces the document list on a backlog item.</summary>
public sealed class UpdateBacklogDocumentsCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateBacklogDocumentsCommand, Result>
{
    /// <summary>Validates permission, replaces the document list, and commits.</summary>
    /// <param name="command">The update documents command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; failure result when the caller lacks permission.</returns>
    public async Task<Result> Handle(UpdateBacklogDocumentsCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageBacklog, ct))
            throw new ForbiddenException("You do not have permission to manage backlog items in this repository.");

        var item = await db.Set<BacklogItem>().AsTracking()
            .FirstOrDefaultAsync(b => b.Id == command.ItemId && b.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(BacklogItem), command.ItemId);

        item.Documents = [.. command.Documents];
        item.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
