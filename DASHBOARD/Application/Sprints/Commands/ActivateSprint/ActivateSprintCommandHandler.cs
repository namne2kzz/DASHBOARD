using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Sprints.Commands.ActivateSprint;

/// <summary>Handles <see cref="ActivateSprintCommand"/>: enforces the one-active-sprint rule and transitions status to Active.</summary>
public sealed class ActivateSprintCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<ActivateSprintCommand, Result>
{
    /// <summary>Validates permission, checks no other sprint is active, then activates the target sprint.</summary>
    /// <param name="command">The activate command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; failure if another sprint is already active.</returns>
    public async Task<Result> Handle(ActivateSprintCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageSprint, ct))
            throw new ForbiddenException("You do not have permission to manage sprints in this repository.");

        var sprint = await db.Set<Sprint>()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.Id == command.SprintId && s.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(Sprint), command.SprintId);

        if (sprint.Status == SprintStatus.Active)
            return Result.Failure("Sprint is already active.");

        if (sprint.Status == SprintStatus.Closed)
            return Result.Failure("Cannot activate a closed sprint.");

        var hasActive = await db.Set<Sprint>()
            .AnyAsync(s => s.RepositoryId == command.RepositoryId
                        && s.Id           != command.SprintId
                        && s.Status       == SprintStatus.Active, ct);

        if (hasActive)
            return Result.Failure("Another sprint is already active in this repository. Close it before activating a new one.");

        sprint.Status = SprintStatus.Active;
        sprint.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
