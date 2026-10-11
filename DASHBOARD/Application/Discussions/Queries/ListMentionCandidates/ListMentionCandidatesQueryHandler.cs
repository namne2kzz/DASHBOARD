using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Discussions.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Discussions.Queries.ListMentionCandidates;

/// <summary>
/// Handles <see cref="ListMentionCandidatesQuery"/>: returns the sprint's capacity members when the task
/// belongs to a sprint, otherwise falls back to the repository's members.
/// </summary>
public sealed class ListMentionCandidatesQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListMentionCandidatesQuery, IReadOnlyList<MentionCandidateDto>>
{
    /// <summary>Validates membership, resolves the task's sprint, and returns the mentionable users.</summary>
    /// <param name="query">The candidates query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Mentionable users ordered by name.</returns>
    public async Task<IReadOnlyList<MentionCandidateDto>> Handle(ListMentionCandidatesQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var sprintId = await db.Set<SprintTask>()
            .AsNoTracking()
            .Where(t => t.Id == query.SprintTaskId && t.RepositoryId == query.RepositoryId)
            .Select(t => t.SprintId)
            .FirstOrDefaultAsync(ct);

        // Task is in a sprint → mention from that sprint's capacity planning.
        if (sprintId is { } sid)
        {
            return await db.Set<CapacityMember>()
                .AsNoTracking()
                .Where(c => c.SprintId == sid)
                .OrderBy(c => c.User!.Name)
                .Select(c => new MentionCandidateDto(c.UserId, c.User!.Name, c.User.AvatarClass))
                .ToListAsync(ct);
        }

        // Backlog task with no sprint → fall back to the repository's members.
        return await db.Set<RepositoryMember>()
            .AsNoTracking()
            .Where(m => m.RepositoryId == query.RepositoryId)
            .OrderBy(m => m.User!.Name)
            .Select(m => new MentionCandidateDto(m.UserId, m.User!.Name, m.User.AvatarClass))
            .ToListAsync(ct);
    }
}
