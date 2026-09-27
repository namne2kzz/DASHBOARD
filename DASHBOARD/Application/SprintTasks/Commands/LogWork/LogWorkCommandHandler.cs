using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Commands.LogWork;

/// <summary>Handles <see cref="LogWorkCommand"/>: adds worked hours to CompletedWork and updates RemainingWork, then auto-transitions to Done if remaining reaches zero.</summary>
public sealed class LogWorkCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<LogWorkCommand, Result>
{
    /// <summary>Validates membership, adds hours to CompletedWork, updates RemainingWork, and auto-closes if remaining reaches zero.</summary>
    /// <param name="command">The log-work command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(LogWorkCommand command, CancellationToken ct)
    {
        // Logging work can auto-close the item when remaining work reaches zero, so this needs the
        // same privilege as editing a work item directly — membership alone would let any member
        // close items through the back door.
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.EditWorkItem, ct))
            throw new ForbiddenException("You do not have permission to edit work items in this repository.");

        if (command.HoursWorked <= 0)
            return Result.Failure("HoursWorked must be greater than zero.");

        if (command.RemainingWork < 0)
            return Result.Failure("RemainingWork cannot be negative.");

        var task = await db.Set<SprintTask>()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Id == command.TaskId && t.SprintId == command.SprintId, ct)
            ?? throw new NotFoundException(nameof(SprintTask), command.TaskId);

        task.CompletedWork  += command.HoursWorked;
        task.RemainingWork   = command.RemainingWork;

        if (task.RemainingWork == 0 && task.State != Domain.Enums.SprintTaskState.Done)
        {
            task.State    = Domain.Enums.SprintTaskState.Done;
            task.ClosedAt = DateTime.UtcNow;
        }

        task.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
