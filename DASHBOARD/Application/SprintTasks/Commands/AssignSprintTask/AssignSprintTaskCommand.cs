using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Commands.AssignSprintTask;

/// <summary>Assigns (or unassigns) a sprint task to a team member.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint containing the task.</param>
/// <param name="TaskId">The task to assign.</param>
/// <param name="AssignedToId">The member to assign to, or null to unassign.</param>
public sealed record AssignSprintTaskCommand(
    Guid  RepositoryId,
    Guid  SprintId,
    Guid  TaskId,
    Guid? AssignedToId) : IRequest<Result>;
