using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.SprintTasks.Queries.ListSprintTasks;

/// <summary>Handles <see cref="ListSprintTasksQuery"/>: loads all tasks for a sprint and builds the parent-child tree.</summary>
public sealed class ListSprintTasksQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListSprintTasksQuery, IReadOnlyList<SprintTaskDto>>
{
    /// <summary>Validates membership, loads tasks with assignees, and returns the nested tree.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Root-level tasks with sub-tasks nested inside.</returns>
    public async Task<IReadOnlyList<SprintTaskDto>> Handle(ListSprintTasksQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var repoCode = await db.Set<Repository>()
            .Where(r => r.Id == query.RepositoryId).Select(r => r.Code).FirstAsync(ct);

        var all = await db.Set<SprintTask>()
            .AsNoTracking()
            .Include(t => t.AssignedTo)
            .Where(t => t.SprintId == query.SprintId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        return BuildTree(all, null, repoCode);
    }

    internal static IReadOnlyList<SprintTaskDto> BuildTree(List<SprintTask> all, Guid? parentId, string repoCode)
        => all
            .Where(t => t.ParentId == parentId)
            .Select(t => MapToDto(t, repoCode, BuildTree(all, t.Id, repoCode)))
            .ToList();

    internal static SprintTaskDto MapToDto(SprintTask t, string repoCode, IReadOnlyList<SprintTaskDto> subTasks,
        string? parentWorkItemNumber = null, string? parentTitle = null)
        => new(t.Id, t.SprintId, t.RepositoryId, t.BacklogItemId, t.ParentId, parentWorkItemNumber, parentTitle,
               SprintTask.BuildWorkItemNumber(repoCode, t.WorkItemNumber),
               t.Type, t.Title, t.Description, t.Priority,
               t.AssignedToId, t.AssignedTo?.Name, t.AssignedTo?.AvatarClass,
               t.State, t.StoryPoints, t.OriginalEstimate, t.RemainingWork, t.CompletedWork, t.ClosedAt,
               t.AcceptanceCriteria, t.Documents,
               t.StepsToReproduce, t.Environment, t.RootCause, t.Solution, t.Impaction,
               t.UnitTest, t.DesignReview, t.TestSteps, t.Automated, subTasks);
}
