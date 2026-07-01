using DASHBOARD.Application.SprintTasks.DTOs;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Queries.SearchParentStories;

/// <summary>Searches User Story work items in a repository, for the "Parent" picker on the create-work-item dialog.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SearchTerm">Optional title or work item number fragment to match.</param>
public sealed record SearchParentStoriesQuery(
    Guid    RepositoryId,
    string? SearchTerm) : IRequest<IReadOnlyList<WorkItemPickerDto>>;
