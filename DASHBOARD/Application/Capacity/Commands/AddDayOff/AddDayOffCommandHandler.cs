using DASHBOARD.Application.Capacity.DTOs;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Capacity.Commands.AddDayOff;

/// <summary>Handles <see cref="AddDayOffCommand"/>: validates sprint date range constraint then creates the day-off entry.</summary>
public sealed class AddDayOffCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<AddDayOffCommand, Result<DayOffDto>>
{
    /// <summary>Validates permission, ensures the date is within the sprint range, creates the entry, and returns the DTO.</summary>
    /// <param name="command">The add command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="DayOffDto"/>, or a failure if permission is denied or the date is out of range.</returns>
    public async Task<Result<DayOffDto>> Handle(AddDayOffCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageCapacity, ct))
            throw new ForbiddenException("You do not have permission to manage capacity in this repository.");

        var sprint = await db.Set<Sprint>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == command.SprintId && s.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(Sprint), command.SprintId);

        if (command.Date < sprint.StartDate || command.Date > sprint.EndDate)
            return Result<DayOffDto>.Failure(
                $"Day-off date {command.Date} is outside the sprint range ({sprint.StartDate} to {sprint.EndDate}).");

        string? userName = null;
        if (command.UserId.HasValue)
        {
            var dayOffUser = await db.Set<User>().AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == command.UserId, ct)
                ?? throw new NotFoundException(nameof(User), command.UserId.Value);
            userName = dayOffUser.Name;
        }

        var dayOff = new DayOff
        {
            SprintId     = command.SprintId,
            RepositoryId = command.RepositoryId,
            UserId       = command.UserId,
            Date         = command.Date,
            Hours        = command.Hours,
            Reason       = command.Reason,
        };
        db.Set<DayOff>().Add(dayOff);
        await uow.CommitAsync(ct);

        return Result<DayOffDto>.Success(new DayOffDto(dayOff.Id, dayOff.SprintId, dayOff.UserId, userName, dayOff.Date, dayOff.Hours, dayOff.Reason));
    }
}
