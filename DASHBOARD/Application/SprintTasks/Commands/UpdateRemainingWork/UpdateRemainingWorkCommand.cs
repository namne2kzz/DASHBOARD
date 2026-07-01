using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Commands.UpdateRemainingWork;

/// <summary>Updates the remaining work estimate on a task without logging completed hours.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint containing the task.</param>
/// <param name="TaskId">The task to update.</param>
/// <param name="RemainingWork">New remaining hours (≥ 0).</param>
public sealed record UpdateRemainingWorkCommand(
    Guid    RepositoryId,
    Guid    SprintId,
    Guid    TaskId,
    decimal RemainingWork) : IRequest<Result>;
