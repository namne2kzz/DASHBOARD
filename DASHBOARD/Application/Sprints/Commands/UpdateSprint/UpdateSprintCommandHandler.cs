using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Sprints.Commands.UpdateSprint;

/// <summary>Handles <see cref="UpdateSprintCommand"/>: checks ManageSprint permission, validates dates, applies changes.</summary>
public sealed class UpdateSprintCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateSprintCommand, Result>
{
    /// <summary>Validates permission and date order, then updates the sprint.</summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(UpdateSprintCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageSprint, ct))
            return Result.Failure("You do not have permission to manage sprints in this repository.");

        if (command.EndDate <= command.StartDate)
            return Result.Failure("EndDate must be after StartDate.");

        var overlaps = await db.Set<Sprint>()
            .AnyAsync(s => s.RepositoryId == command.RepositoryId
                        && s.Id           != command.SprintId
                        && s.StartDate    <= command.EndDate
                        && s.EndDate      >= command.StartDate, ct);

        if (overlaps)
            return Result.Failure("Sprint dates overlap with an existing sprint in this repository.");

        var sprint = await db.Set<Sprint>().AsTracking()
            .FirstOrDefaultAsync(s => s.Id == command.SprintId && s.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(Sprint), command.SprintId);

        sprint.Name      = command.Name;
        sprint.StartDate = command.StartDate;
        sprint.EndDate   = command.EndDate;
        sprint.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
