using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Sprints.Commands.CloseSprint;

/// <summary>
/// Handles <see cref="CloseSprintCommand"/>: checks for incomplete items, returns a warning when Force is false,
/// or closes the sprint when Force is true or all items are done.
/// </summary>
public sealed class CloseSprintCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<CloseSprintCommand, Result<CloseSprintResult>>
{
    /// <summary>
    /// Validates permission, counts non-Done root items, returns a warning result when incomplete items exist
    /// and Force is false; otherwise sets Status=Closed and stamps ClosedAt.
    /// </summary>
    /// <param name="command">The close command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CloseSprintResult"/> indicating outcome and incomplete-item count.</returns>
    public async Task<Result<CloseSprintResult>> Handle(CloseSprintCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageSprint, ct))
            throw new ForbiddenException("You do not have permission to manage sprints in this repository.");

        var sprint = await db.Set<Sprint>()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.Id == command.SprintId && s.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(Sprint), command.SprintId);

        if (sprint.Status == SprintStatus.Closed)
            return Result<CloseSprintResult>.Failure("Sprint is already closed.");

        if (sprint.Status == SprintStatus.Planning)
            return Result<CloseSprintResult>.Failure("Cannot close a sprint that has not been activated yet.");

        // Count root-level items that haven't reached a terminal state.
        // Category is a computed property — must evaluate in memory; load only State column.
        var states = await db.Set<SprintTask>()
            .AsNoTracking()
            .Where(t => t.SprintId == command.SprintId
                     && t.RepositoryId == command.RepositoryId
                     && t.ParentId == null
                     && !t.IsDeleted)
            .Select(t => t.State)
            .ToListAsync(ct);

        // Terminal states: Done(6), Passed(7), Failed(8), Closed(9)
        var incompleteCount = states.Count(s => (int)s < 6);

        if (incompleteCount > 0 && !command.Force)
        {
            return Result<CloseSprintResult>.Success(new CloseSprintResult(
                Closed:             false,
                IncompleteItemCount: incompleteCount,
                Warning: $"{incompleteCount} item{(incompleteCount == 1 ? "" : "s")} still in progress. Close anyway?"));
        }

        sprint.Status   = SprintStatus.Closed;
        sprint.ClosedAt = DateTime.UtcNow;
        sprint.Touch();

        await uow.CommitAsync(ct);

        return Result<CloseSprintResult>.Success(new CloseSprintResult(
            Closed:             true,
            IncompleteItemCount: incompleteCount));
    }
}
