using DASHBOARD.Application.SprintTasks.DTOs;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Queries.GetSprintTaskMetadata;

/// <summary>Returns the metadata catalog values (labels, components, versions, …) assigned to a work item.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The work item to read.</param>
public sealed record GetSprintTaskMetadataQuery(Guid RepositoryId, Guid SprintTaskId)
    : IRequest<IReadOnlyList<WorkItemMetadataDto>>;
