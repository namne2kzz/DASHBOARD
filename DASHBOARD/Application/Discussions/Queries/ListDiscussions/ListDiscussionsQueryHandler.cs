using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Discussions.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Discussions.Queries.ListDiscussions;

/// <summary>Handles <see cref="ListDiscussionsQuery"/>: checks membership then loads roots with their first reply.</summary>
public sealed class ListDiscussionsQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListDiscussionsQuery, IReadOnlyList<DiscussionDto>>
{
    /// <summary>Validates membership, loads root comments plus each one's first reply, and returns them in thread order.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Root comments oldest-first, each followed by its first reply when it has one.</returns>
    public async Task<IReadOnlyList<DiscussionDto>> Handle(ListDiscussionsQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var isAdmin = await user.IsGlobalAdminAsync(ct);

        var roots = await db.Set<DiscussionEntry>()
            .AsNoTracking()
            .Where(d => d.SprintTaskId == query.SprintTaskId
                     && d.RepositoryId == query.RepositoryId
                     && d.ParentId == null)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new
            {
                Entry        = d,
                AuthorName   = d.Author!.Name,
                AuthorAvatar = d.Author.AvatarClass,
                ReplyCount   = d.Replies.Count,
                // Only the first reply travels with the root; "show more" fetches the rest.
                FirstReply   = d.Replies
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new
                    {
                        Entry        = r,
                        AuthorName   = r.Author!.Name,
                        AuthorAvatar = r.Author.AvatarClass,
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var thread = new List<DiscussionDto>(roots.Count);
        foreach (var root in roots)
        {
            thread.Add(Map(root.Entry, root.AuthorName, root.AuthorAvatar, root.ReplyCount, isAdmin));

            if (root.FirstReply is { } reply)
                thread.Add(Map(reply.Entry, reply.AuthorName, reply.AuthorAvatar, 0, isAdmin));
        }

        return thread;
    }

    /// <summary>Projects an entry plus its author details into a DTO, stamping the caller's permissions.</summary>
    private DiscussionDto Map(DiscussionEntry d, string authorName, string authorAvatar, int replyCount, bool isAdmin) =>
        new(d.Id, d.SprintTaskId, d.ParentId, d.AuthorId,
            authorName, authorAvatar, d.Body, d.CreatedAt, d.UpdatedAt,
            replyCount,
            CanEdit:   user.IsSelf(d.AuthorId),
            CanDelete: user.IsSelf(d.AuthorId) || isAdmin);
}
