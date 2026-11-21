using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Discussions.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Discussions.Queries.ListReplies;

/// <summary>Handles <see cref="ListRepliesQuery"/>: checks membership then returns one page of replies.</summary>
public sealed class ListRepliesQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListRepliesQuery, IReadOnlyList<DiscussionDto>>
{
    /// <summary>Largest page a caller can request, whatever Take says.</summary>
    private const int MaxTake = 50;

    /// <summary>Validates membership and returns the requested slice of replies, oldest-first.</summary>
    /// <param name="query">The replies query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The requested page of replies.</returns>
    public async Task<IReadOnlyList<DiscussionDto>> Handle(ListRepliesQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var isAdmin = await user.IsGlobalAdminAsync(ct);
        var take    = Math.Clamp(query.Take, 1, MaxTake);
        var skip    = Math.Max(0, query.Skip);

        return await db.Set<DiscussionEntry>()
            .AsNoTracking()
            .Where(d => d.SprintTaskId == query.SprintTaskId
                     && d.RepositoryId == query.RepositoryId
                     && d.ParentId     == query.ParentId)
            .OrderBy(d => d.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(d => new DiscussionDto(
                d.Id, d.SprintTaskId, d.ParentId, d.AuthorId,
                d.Author!.Name, d.Author.AvatarClass,
                d.Body, d.CreatedAt, d.UpdatedAt,
                0,
                user.IsSelf(d.AuthorId),
                user.IsSelf(d.AuthorId) || isAdmin))
            .ToListAsync(ct);
    }
}
