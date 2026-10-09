using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Sprints.Commands.CloseSprint;

/// <summary>Closes a sprint. When <paramref name="Force"/> is false the handler returns a warning if incomplete items remain.</summary>
public sealed record CloseSprintCommand(
    Guid RepositoryId,
    Guid SprintId,
    /// <summary>When true, close the sprint even if incomplete items remain.</summary>
    bool Force = false) : IRequest<Result<CloseSprintResult>>;

/// <summary>Outcome of a close-sprint operation.</summary>
public sealed record CloseSprintResult(
    /// <summary>Whether the sprint was actually closed (false when <see cref="IncompleteItemCount"/> &gt; 0 and Force was false).</summary>
    bool  Closed,
    /// <summary>Number of items whose <see cref="DASHBOARD.Domain.Entities.SprintTask.Category"/> is not Done.</summary>
    int   IncompleteItemCount,
    /// <summary>Human-readable warning message; null when the sprint was closed cleanly.</summary>
    string? Warning = null);
