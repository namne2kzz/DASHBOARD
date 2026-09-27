using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Queries.SearchParentStories;

/// <summary>Handles <see cref="SearchParentStoriesQuery"/>: returns up to 20 User Story items matching the search term.</summary>
public sealed class SearchParentStoriesQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<SearchParentStoriesQuery, IReadOnlyList<WorkItemPickerDto>>
{
    /// <summary>Validates membership, loads User Story items for the repo, and filters by title or formatted work item number.</summary>
    /// <param name="query">The search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Up to 20 matching <see cref="WorkItemPickerDto"/>, most recent first.</returns>
    public async Task<IReadOnlyList<WorkItemPickerDto>> Handle(SearchParentStoriesQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var repoCode = await db.Set<Repository>()
            .Where(r => r.Id == query.RepositoryId).Select(r => r.Code).FirstAsync(ct);

        var stories = await db.Set<SprintTask>()
            .AsNoTracking()
            .Where(t => t.RepositoryId == query.RepositoryId && t.Type == SprintTaskType.UserStory)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new { t.Id, t.WorkItemNumber, t.Title })
            .ToListAsync(ct);

        var term = query.SearchTerm?.Trim().ToLower();

        return stories
            .Select(t => new WorkItemPickerDto(t.Id, SprintTask.BuildWorkItemNumber(repoCode, t.WorkItemNumber), t.Title))
            .Where(d => string.IsNullOrEmpty(term) ||
                        d.Title.ToLower().Contains(term) ||
                        d.WorkItemNumber.ToLower().Contains(term))
            .Take(20)
            .ToList();
    }
}
