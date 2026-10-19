using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.WorkItemLinks;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.WorkItemLinks.Commands.DeleteWorkItemLink;

/// <summary>Handles <see cref="DeleteWorkItemLinkCommand"/>: removes the link and audits both items.</summary>
public sealed class DeleteWorkItemLinkCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IHistoryService       history,
    IUnitOfWork           uow) : IRequestHandler<DeleteWorkItemLinkCommand, Result>
{
    /// <summary>Validates membership, deletes the link, and records history on both sides.</summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(DeleteWorkItemLinkCommand command, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(command.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var link = await db.Set<WorkItemLink>()
            .FirstOrDefaultAsync(l => l.Id == command.LinkId && l.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(WorkItemLink), command.LinkId);

        var repoCode = await db.Set<Repository>().AsNoTracking()
            .Where(r => r.Id == command.RepositoryId).Select(r => r.Code).FirstOrDefaultAsync(ct);

        var keys = await db.Set<SprintTask>().AsNoTracking()
            .Where(t => t.Id == link.SourceTaskId || t.Id == link.TargetTaskId)
            .Select(t => new { t.Id, t.WorkItemNumber })
            .ToListAsync(ct);

        db.Set<WorkItemLink>().Remove(link);

        if (repoCode is not null)
        {
            var sourceNum = keys.FirstOrDefault(k => k.Id == link.SourceTaskId)?.WorkItemNumber;
            var targetNum = keys.FirstOrDefault(k => k.Id == link.TargetTaskId)?.WorkItemNumber;
            var outward   = WorkItemLinkLabels.For(link.LinkType, outward: true);
            var inward    = WorkItemLinkLabels.For(link.LinkType, outward: false);

            if (targetNum is not null)
                history.Record(link.SourceTaskId, command.RepositoryId, user.UserId,
                    $"Removed link: {outward} {SprintTask.BuildWorkItemNumber(repoCode, targetNum.Value)}.");
            if (sourceNum is not null)
                history.Record(link.TargetTaskId, command.RepositoryId, user.UserId,
                    $"Removed link: {inward} {SprintTask.BuildWorkItemNumber(repoCode, sourceNum.Value)}.");
        }

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
