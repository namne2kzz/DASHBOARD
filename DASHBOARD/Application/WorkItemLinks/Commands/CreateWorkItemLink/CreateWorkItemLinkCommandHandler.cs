using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.WorkItemLinks.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.WorkItemLinks.Commands.CreateWorkItemLink;

/// <summary>
/// Handles <see cref="CreateWorkItemLinkCommand"/>: validates both items, prevents self/duplicate links,
/// creates the link, and records history on both sides.
/// </summary>
public sealed class CreateWorkItemLinkCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IHistoryService       history,
    IUnitOfWork           uow) : IRequestHandler<CreateWorkItemLinkCommand, Result<WorkItemLinkDto>>
{
    /// <summary>Validates and creates the link, then returns it from the source item's perspective.</summary>
    /// <param name="command">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created <see cref="WorkItemLinkDto"/>; failure on a business-rule violation.</returns>
    public async Task<Result<WorkItemLinkDto>> Handle(CreateWorkItemLinkCommand command, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(command.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        if (command.SourceTaskId == command.TargetTaskId)
            return Result<WorkItemLinkDto>.Failure("A work item cannot be linked to itself.");

        var repoCode = await db.Set<Repository>().AsNoTracking()
            .Where(r => r.Id == command.RepositoryId)
            .Select(r => r.Code)
            .FirstOrDefaultAsync(ct);
        if (repoCode is null)
            return Result<WorkItemLinkDto>.Failure("Repository not found.");

        var tasks = await db.Set<SprintTask>().AsNoTracking()
            .Where(t => (t.Id == command.SourceTaskId || t.Id == command.TargetTaskId)
                        && t.RepositoryId == command.RepositoryId && !t.IsDeleted)
            .Select(t => new { t.Id, t.WorkItemNumber, t.Title, t.Type, t.State })
            .ToListAsync(ct);

        var source = tasks.FirstOrDefault(t => t.Id == command.SourceTaskId);
        var target = tasks.FirstOrDefault(t => t.Id == command.TargetTaskId);
        if (source is null || target is null)
            return Result<WorkItemLinkDto>.Failure("One or both work items were not found in this repository.");

        // Reject duplicates in either stored direction.
        var exists = await db.Set<WorkItemLink>().AnyAsync(l =>
            l.LinkType == command.LinkType &&
            ((l.SourceTaskId == command.SourceTaskId && l.TargetTaskId == command.TargetTaskId) ||
             (l.SourceTaskId == command.TargetTaskId && l.TargetTaskId == command.SourceTaskId)), ct);
        if (exists)
            return Result<WorkItemLinkDto>.Failure("This link already exists.");

        var link = new WorkItemLink
        {
            SourceTaskId = command.SourceTaskId,
            TargetTaskId = command.TargetTaskId,
            RepositoryId = command.RepositoryId,
            LinkType     = command.LinkType,
            Description  = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            CreatedById  = user.UserId,
        };
        db.Set<WorkItemLink>().Add(link);

        var sourceKey = SprintTask.BuildWorkItemNumber(repoCode, source.WorkItemNumber);
        var targetKey = SprintTask.BuildWorkItemNumber(repoCode, target.WorkItemNumber);
        var outward   = WorkItemLinkLabels.For(command.LinkType, outward: true);
        var inward    = WorkItemLinkLabels.For(command.LinkType, outward: false);

        // Audit both items so the relationship shows up in either history.
        history.Record(command.SourceTaskId, command.RepositoryId, user.UserId, $"Linked: {outward} {targetKey}.");
        history.Record(command.TargetTaskId, command.RepositoryId, user.UserId, $"Linked: {inward} {sourceKey}.");

        await uow.CommitAsync(ct);

        return Result<WorkItemLinkDto>.Success(new WorkItemLinkDto(
            link.Id, command.LinkType.ToString(), outward,
            target.Id, targetKey, target.Title, target.Type.ToString(), target.State.ToString(),
            link.Description));
    }
}
