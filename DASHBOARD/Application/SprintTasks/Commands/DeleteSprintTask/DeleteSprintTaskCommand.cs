using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Commands.DeleteSprintTask;

/// <summary>Soft-deletes a work item. Root UserStory items also remove their child sub-tasks.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="TaskId">The work item to delete.</param>
public sealed record DeleteSprintTaskCommand(Guid RepositoryId, Guid TaskId) : IRequest<Result>;
