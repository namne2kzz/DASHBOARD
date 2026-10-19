using DASHBOARD.Application.SprintTasks.DTOs;
using MediatR;

namespace DASHBOARD.Application.WorkItemLinks.Queries.SearchLinkTargets;

/// <summary>Searches work items in a repository to link to, excluding the current item.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="ExcludeTaskId">The current work item, excluded from results.</param>
/// <param name="SearchTerm">Optional title/key filter.</param>
public sealed record SearchLinkTargetsQuery(
    Guid    RepositoryId,
    Guid    ExcludeTaskId,
    string? SearchTerm) : IRequest<IReadOnlyList<WorkItemPickerDto>>;
