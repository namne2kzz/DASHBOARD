using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Sprints.Commands.DeleteSprint;

/// <summary>Handles <see cref="DeleteSprintCommand"/>: blocks deletion when tasks are committed, then hard-deletes the sprint.</summary>
public sealed class DeleteSprintCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<DeleteSprintCommand, Result>
{
    /// <summary>Validates permission, checks for committed tasks, and deletes the sprint.</summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; <see cref="Result.Failure"/> when tasks block deletion.</returns>
    public async Task<Result> Handle(DeleteSprintCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageSprint, ct))
            throw new ForbiddenException("You do not have permission to manage sprints in this repository.");

        var sprint = await db.Set<Sprint>().AsTracking()
            .FirstOrDefaultAsync(s => s.Id == command.SprintId && s.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(Sprint), command.SprintId);

        if (sprint.Status == SprintStatus.Active)
            return Result.Failure("Cannot delete an active sprint. Close it first.");

        var taskCount = await db.Set<SprintTask>()
            .CountAsync(t => t.SprintId == command.SprintId, ct);
        if (taskCount > 0)
            return Result.Failure($"Cannot delete sprint with {taskCount} committed task(s). Remove tasks first.");

        db.Set<Sprint>().Remove(sprint);
        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
