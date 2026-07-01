using DASHBOARD.Application.SprintTasks.DTOs;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Queries.GetSprintTaskDetail;

/// <summary>Loads a single sprint task by ID for the task detail dialog.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="TaskId">The sprint task to load.</param>
public sealed record GetSprintTaskDetailQuery(
    Guid RepositoryId,
    Guid TaskId) : IRequest<SprintTaskDto>;
