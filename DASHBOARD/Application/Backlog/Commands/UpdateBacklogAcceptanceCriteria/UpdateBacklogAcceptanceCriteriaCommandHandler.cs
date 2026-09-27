using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogAcceptanceCriteria;

/// <summary>Handles <see cref="UpdateBacklogAcceptanceCriteriaCommand"/>: updates the acceptance criteria field of a backlog item.</summary>
public sealed class UpdateBacklogAcceptanceCriteriaCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateBacklogAcceptanceCriteriaCommand, Result>
{
    /// <summary>Validates permission, applies the new acceptance criteria, and commits.</summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; failure result when the caller lacks permission.</returns>
    public async Task<Result> Handle(UpdateBacklogAcceptanceCriteriaCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageBacklog, ct))
            throw new ForbiddenException("You do not have permission to manage backlog items in this repository.");

        var item = await db.Set<BacklogItem>().AsTracking()
            .FirstOrDefaultAsync(b => b.Id == command.ItemId && b.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(BacklogItem), command.ItemId);

        item.AcceptanceCriteria = command.AcceptanceCriteria;
        item.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
