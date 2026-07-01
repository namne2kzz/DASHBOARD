using DASHBOARD.Application.SprintTasks.DTOs;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Queries.ListSprintTasks;

/// <summary>Returns all sprint tasks for a sprint, built as a tree (root tasks contain their sub-tasks).</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint to query.</param>
public sealed record ListSprintTasksQuery(Guid RepositoryId, Guid SprintId) : IRequest<IReadOnlyList<SprintTaskDto>>;
