using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Capacity.Commands.RemoveDayOff;

/// <summary>Handles <see cref="RemoveDayOffCommand"/>: checks ManageCapacity permission then removes the day-off entry.</summary>
public sealed class RemoveDayOffCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<RemoveDayOffCommand, Result>
{
    /// <summary>Validates permission and removes the day-off entry.</summary>
    /// <param name="command">The remove command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(RemoveDayOffCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageCapacity, ct))
            return Result.Failure("You do not have permission to manage capacity in this repository.");

        var dayOff = await db.Set<DayOff>()
            .FirstOrDefaultAsync(d => d.Id == command.DayOffId && d.SprintId == command.SprintId, ct)
            ?? throw new NotFoundException(nameof(DayOff), command.DayOffId);

        db.Set<DayOff>().Remove(dayOff);
        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
