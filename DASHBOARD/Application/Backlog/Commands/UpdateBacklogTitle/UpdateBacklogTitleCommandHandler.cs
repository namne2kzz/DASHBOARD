using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogTitle;

/// <summary>Handles <see cref="UpdateBacklogTitleCommand"/>: renames a backlog item.</summary>
public sealed class UpdateBacklogTitleCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateBacklogTitleCommand, Result>
{
    /// <summary>Validates permission, applies the new title, and commits.</summary>
    /// <param name="command">The rename command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; failure result when the caller lacks permission.</returns>
    public async Task<Result> Handle(UpdateBacklogTitleCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.EditWorkItem, ct))
            return Result.Failure("You do not have permission to edit backlog items in this repository.");

        var item = await db.Set<BacklogItem>().AsTracking()
            .FirstOrDefaultAsync(b => b.Id == command.ItemId && b.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(BacklogItem), command.ItemId);

        item.Title = command.Title;
        item.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
