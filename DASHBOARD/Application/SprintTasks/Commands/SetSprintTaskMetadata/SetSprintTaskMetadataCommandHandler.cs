using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Commands.SetSprintTaskMetadata;

/// <summary>
/// Handles <see cref="SetSprintTaskMetadataCommand"/>: replaces a work item's metadata assignments with the
/// supplied set, keeping only IDs that resolve to catalog values in this repository (or global entries).
/// </summary>
public sealed class SetSprintTaskMetadataCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<SetSprintTaskMetadataCommand, Result>
{
    /// <summary>Validates permission and inputs, then swaps the assignment set in a single commit.</summary>
    /// <param name="command">The set-metadata command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success; failure on permission violation.</returns>
    public async Task<Result> Handle(SetSprintTaskMetadataCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.EditWorkItem, ct))
            throw new ForbiddenException("You do not have permission to edit work items in this repository.");

        var task = await db.Set<SprintTask>().AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == command.SprintTaskId && t.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(SprintTask), command.SprintTaskId);

        // Keep only IDs that are real catalog values for this repository (or global).
        var requested = command.MetadataIds.Distinct().ToList();
        var validIds = await db.Set<RepositoryMetadata>().AsNoTracking()
            .Where(m => requested.Contains(m.Id) && (m.IsGlobal || m.RepositoryId == command.RepositoryId))
            .Select(m => m.Id)
            .ToListAsync(ct);

        var existing = await db.Set<WorkItemMetadata>().AsTracking()
            .Where(w => w.SprintTaskId == command.SprintTaskId)
            .ToListAsync(ct);

        var existingIds = existing.Select(e => e.MetadataId).ToHashSet();
        var desiredIds  = validIds.ToHashSet();

        var toRemove = existing.Where(e => !desiredIds.Contains(e.MetadataId)).ToList();
        if (toRemove.Count > 0)
            db.Set<WorkItemMetadata>().RemoveRange(toRemove);

        foreach (var id in desiredIds.Where(id => !existingIds.Contains(id)))
            db.Set<WorkItemMetadata>().Add(new WorkItemMetadata { SprintTaskId = command.SprintTaskId, MetadataId = id });

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
