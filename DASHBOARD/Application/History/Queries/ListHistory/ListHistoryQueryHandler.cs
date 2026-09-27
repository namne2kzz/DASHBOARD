using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.History.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.History.Queries.ListHistory;

/// <summary>Handles <see cref="ListHistoryQuery"/>: checks membership then loads audit-trail entries with author details.</summary>
public sealed class ListHistoryQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListHistoryQuery, IReadOnlyList<HistoryDto>>
{
    /// <summary>Validates membership, loads history entries with authors, and returns newest-first DTOs.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All history entries for the task ordered newest-first.</returns>
    public async Task<IReadOnlyList<HistoryDto>> Handle(ListHistoryQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        return await db.Set<HistoryEntry>()
            .AsNoTracking()
            .Include(h => h.Author)
            .Where(h => h.SprintTaskId == query.SprintTaskId && h.RepositoryId == query.RepositoryId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new HistoryDto(
                h.Id, h.SprintTaskId, h.AuthorId,
                h.Author!.Name, h.Author.AvatarClass,
                h.Message, h.CreatedAt))
            .ToListAsync(ct);
    }
}
