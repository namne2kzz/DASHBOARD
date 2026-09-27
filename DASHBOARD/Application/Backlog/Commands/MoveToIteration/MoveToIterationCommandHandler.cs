using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Commands.MoveToIteration;

/// <summary>Handles <see cref="MoveToIterationCommand"/>: sets (or clears) the sprint planning assignment without changing refinement state.</summary>
public sealed class MoveToIterationCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<MoveToIterationCommand, Result>
{
    /// <summary>Validates permission, verifies the sprint belongs to the repository, applies the assignment, and commits.</summary>
    /// <param name="command">The move-to-iteration command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; failure result when the caller lacks permission or sprint is not found.</returns>
    public async Task<Result> Handle(MoveToIterationCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageBacklog, ct))
            throw new ForbiddenException("You do not have permission to manage backlog items in this repository.");

        if (command.SprintId.HasValue &&
            !await db.Set<Sprint>().AnyAsync(s => s.Id == command.SprintId && s.RepositoryId == command.RepositoryId, ct))
            return Result.Failure("Sprint not found in this repository.");

        var item = await db.Set<BacklogItem>().AsTracking()
            .FirstOrDefaultAsync(b => b.Id == command.ItemId && b.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(BacklogItem), command.ItemId);

        item.SprintId = command.SprintId;
        item.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
