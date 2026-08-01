using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Commands.SetSprintTaskMetadata;

/// <summary>Replaces the full set of metadata catalog values assigned to a work item.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintTaskId">The work item to update.</param>
/// <param name="MetadataIds">The complete desired set of repository metadata value IDs.</param>
public sealed record SetSprintTaskMetadataCommand(
    Guid                RepositoryId,
    Guid                SprintTaskId,
    IReadOnlyList<Guid> MetadataIds) : IRequest<Result>;
