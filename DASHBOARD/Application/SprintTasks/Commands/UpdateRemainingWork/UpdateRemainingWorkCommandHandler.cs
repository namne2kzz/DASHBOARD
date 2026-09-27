using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Commands.UpdateRemainingWork;

/// <summary>Handles <see cref="UpdateRemainingWorkCommand"/>: updates remaining hours and auto-closes the task when it reaches zero.</summary>
public sealed class UpdateRemainingWorkCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<UpdateRemainingWorkCommand, Result>
{
    /// <summary>Validates membership, updates RemainingWork, auto-transitions to Done at zero, and commits.</summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(UpdateRemainingWorkCommand command, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(command.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        if (command.RemainingWork < 0)
            return Result.Failure("RemainingWork cannot be negative.");

        var task = await db.Set<SprintTask>()
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Id == command.TaskId && t.SprintId == command.SprintId, ct)
            ?? throw new NotFoundException(nameof(SprintTask), command.TaskId);

        task.RemainingWork = command.RemainingWork;

        if (task.RemainingWork == 0 && task.State != Domain.Enums.SprintTaskState.Done)
            task.State = Domain.Enums.SprintTaskState.Done;

        task.Touch();
        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}