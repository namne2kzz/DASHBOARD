using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Discussions.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Discussions.Queries.ListDiscussions;

/// <summary>Handles <see cref="ListDiscussionsQuery"/>: checks membership then loads comments with author details.</summary>
public sealed class ListDiscussionsQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListDiscussionsQuery, IReadOnlyList<DiscussionDto>>
{
    /// <summary>Validates membership, loads discussion entries with authors, and returns ordered DTOs.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All non-deleted discussion entries ordered oldest-first.</returns>
    public async Task<IReadOnlyList<DiscussionDto>> Handle(ListDiscussionsQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new UnauthorizedAccessException("You are not a member of this repository.");

        return await db.Set<DiscussionEntry>()
            .AsNoTracking()
            .Include(d => d.Author)
            .Where(d => d.SprintTaskId == query.SprintTaskId && d.RepositoryId == query.RepositoryId)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new DiscussionDto(
                d.Id, d.SprintTaskId, d.AuthorId,
                d.Author!.Name, d.Author.AvatarClass,
                d.Body, d.CreatedAt, d.UpdatedAt))
            .ToListAsync(ct);
    }
}
