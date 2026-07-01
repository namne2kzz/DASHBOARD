using DASHBOARD.Application.SprintTasks.DTOs;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Queries.GetBoardTasks;

/// <summary>Returns a flat list of all sprint tasks for kanban board display — no parent/child tree.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint to query.</param>
public sealed record GetBoardTasksQuery(Guid RepositoryId, Guid SprintId)
    : IRequest<IReadOnlyList<BoardTaskDto>>;
