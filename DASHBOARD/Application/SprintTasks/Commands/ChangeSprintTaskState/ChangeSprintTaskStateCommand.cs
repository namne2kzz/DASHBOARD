using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Commands.ChangeSprintTaskState;

/// <summary>Transitions a work item to a new state.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="TaskId">The work item to transition.</param>
/// <param name="NewState">The target state.</param>
public sealed record ChangeSprintTaskStateCommand(
    Guid          RepositoryId,
    Guid          TaskId,
    WorkItemState NewState) : IRequest<Result>;
