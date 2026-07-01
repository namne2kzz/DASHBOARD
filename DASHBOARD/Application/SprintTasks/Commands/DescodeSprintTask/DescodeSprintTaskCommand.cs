using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Commands.DescodeSprintTask;

/// <summary>Removes a sprint task from the sprint and restores its backlog item to Ready state.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint containing the task.</param>
/// <param name="TaskId">The root-level sprint task to descope.</param>
public sealed record DescodeSprintTaskCommand(
    Guid RepositoryId,
    Guid SprintId,
    Guid TaskId) : IRequest<Result>;
