using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.SprintTasks.Commands.LogWork;

/// <summary>Logs completed hours on a sprint task and updates the remaining work. Auto-computes CompletedWork from OriginalEstimate - RemainingWork.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="SprintId">The sprint the task belongs to.</param>
/// <param name="TaskId">The task to log work on.</param>
/// <param name="HoursWorked">Hours completed in this session.</param>
/// <param name="RemainingWork">Updated remaining hours estimate (can be different from calculated value).</param>
public sealed record LogWorkCommand(
    Guid    RepositoryId,
    Guid    SprintId,
    Guid    TaskId,
    decimal HoursWorked,
    decimal RemainingWork) : IRequest<Result>;
