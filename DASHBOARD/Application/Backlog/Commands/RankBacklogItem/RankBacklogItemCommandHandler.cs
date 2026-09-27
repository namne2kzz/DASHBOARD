using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Backlog.Commands.RankBacklogItem;

/// <summary>Handles <see cref="RankBacklogItemCommand"/>: computes a fractional rank midpoint and re-normalizes the sibling list when gaps collapse below the precision threshold.</summary>
public sealed class RankBacklogItemCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<RankBacklogItemCommand, Result>
{
    private const decimal MinGap = 0.001m;

    /// <summary>Validates permission, computes the new fractional rank, re-normalizes when necessary, and commits.</summary>
    /// <param name="command">The rank command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result.Ok"/> on success.</returns>
    public async Task<Result> Handle(RankBacklogItemCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageBacklog, ct))
            throw new ForbiddenException("You do not have permission to reorder backlog items.");

        var item = await db.Set<BacklogItem>().AsTracking()
            .FirstOrDefaultAsync(b => b.Id == command.ItemId && b.RepositoryId == command.RepositoryId, ct)
            ?? throw new NotFoundException(nameof(BacklogItem), command.ItemId);

        // Load siblings at the same level for rank lookup.
        decimal prevRank = 0m;
        decimal nextRank = 0m;
        bool    hasNext  = false;

        if (command.PreviousItemId.HasValue)
        {
            var prev = await db.Set<BacklogItem>().AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == command.PreviousItemId && b.RepositoryId == command.RepositoryId, ct)
                ?? throw new NotFoundException(nameof(BacklogItem), command.PreviousItemId.Value);
            prevRank = prev.Rank;
        }

        if (command.NextItemId.HasValue)
        {
            var next = await db.Set<BacklogItem>().AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == command.NextItemId && b.RepositoryId == command.RepositoryId, ct)
                ?? throw new NotFoundException(nameof(BacklogItem), command.NextItemId.Value);
            nextRank = next.Rank;
            hasNext  = true;
        }

        decimal newRank;
        if (!command.PreviousItemId.HasValue)
            newRank = hasNext ? nextRank / 2m : 1000m;          // top of list
        else if (!hasNext)
            newRank = prevRank + 1000m;                          // bottom of list
        else
            newRank = (prevRank + nextRank) / 2m;

        // Re-normalize the sibling group when precision is exhausted.
        if (hasNext && (nextRank - prevRank) < MinGap)
        {
            var siblings = await db.Set<BacklogItem>().AsTracking()
                .Where(b => b.RepositoryId == command.RepositoryId && b.ParentId == item.ParentId && b.Id != command.ItemId)
                .OrderBy(b => b.Rank)
                .ToListAsync(ct);

            for (int i = 0; i < siblings.Count; i++)
            {
                siblings[i].Rank = (i + 1) * 1000m;
                siblings[i].Touch();
            }
            newRank = (siblings.Count + 1) * 1000m;
        }

        item.Rank = newRank;
        item.Touch();

        await uow.CommitAsync(ct);
        return Result.Ok;
    }
}
