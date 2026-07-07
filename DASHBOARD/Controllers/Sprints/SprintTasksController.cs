using DASHBOARD.Application.SprintTasks.Commands.AssignSprintTask;
using DASHBOARD.Application.SprintTasks.Commands.ChangeSprintTaskState;
using DASHBOARD.Application.SprintTasks.Commands.CreateSprintTask;
using DASHBOARD.Application.SprintTasks.Commands.DescodeSprintTask;
using DASHBOARD.Application.SprintTasks.Commands.LogWork;
using DASHBOARD.Application.SprintTasks.Commands.UpdateRemainingWork;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Application.SprintTasks.Queries.GetBoardTasks;
using DASHBOARD.Application.SprintTasks.Queries.ListSprintTasks;
using DASHBOARD.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.Sprints;

/// <summary>Manages tasks and sub-tasks scoped to a specific sprint.</summary>
[ApiController]
[Route("api/repositories/{repoId:guid}/sprints/{sprintId:guid}/tasks")]
[Authorize]
public sealed class SprintTasksController(ISender mediator) : ControllerBase
{
    /// <summary>Returns all sprint tasks as a hierarchy tree.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with root tasks including nested sub-tasks.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(Guid repoId, Guid sprintId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListSprintTasksQuery(repoId, sprintId), ct);
        return Ok(result);
    }

    /// <summary>Returns all sprint tasks as a flat list for kanban board display.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with a flat <see cref="BoardTaskDto"/> list ordered by type then state.</returns>
    [HttpGet("board")]
    [ProducesResponseType<IReadOnlyList<BoardTaskDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBoardTasks(Guid repoId, Guid sprintId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetBoardTasksQuery(repoId, sprintId), ct);
        return Ok(result);
    }

    /// <summary>Creates a new sprint task or sub-task within a sprint.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="type">Work item type.</param>
    /// <param name="title">Task title.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="priority">Priority level.</param>
    /// <param name="parentId">Parent task ID for sub-tasks; null for root tasks.</param>
    /// <param name="assignedToId">Optional assignee (not applicable for UserStory).</param>
    /// <param name="storyPoints">Story point estimate (UserStory only).</param>
    /// <param name="originalEstimate">Original time estimate in hours.</param>
    /// <param name="stepsToReproduce">Steps to reproduce the issue (Bug only).</param>
    /// <param name="environment">Environment where the bug was observed (Bug only).</param>
    /// <param name="rootCause">Root cause analysis (Bug only).</param>
    /// <param name="solution">Proposed or applied solution (Bug only).</param>
    /// <param name="impaction">Impact description (Bug only).</param>
    /// <param name="unitTest">Unit test notes (Task and Bug only).</param>
    /// <param name="designReview">Design review notes (Task and Bug only).</param>
    /// <param name="testSteps">Ordered test step descriptions (TestPlan only).</param>
    /// <param name="automated">Whether the test plan is automated (TestPlan only).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new <see cref="SprintTaskDto"/>.</returns>
    [HttpPost]
    [ProducesResponseType<SprintTaskDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        Guid repoId, Guid sprintId,
        [FromQuery] SprintTaskType   type,
        [FromQuery] string?          title,
        [FromQuery] string?          description      = null,
        [FromQuery] WorkItemPriority priority         = WorkItemPriority.Medium,
        [FromQuery] Guid?            parentId         = null,
        [FromQuery] Guid?            assignedToId     = null,
        [FromQuery] int              storyPoints      = 0,
        [FromQuery] decimal          originalEstimate = 0m,
        [FromQuery] string?          stepsToReproduce = null,
        [FromQuery] string?          environment      = null,
        [FromQuery] string?          rootCause        = null,
        [FromQuery] string?          solution         = null,
        [FromQuery] string?          impaction        = null,
        [FromQuery] string?          unitTest         = null,
        [FromQuery] string?          designReview     = null,
        [FromQuery] List<string>?    testSteps        = null,
        [FromQuery] bool?            automated        = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new CreateSprintTaskCommand(
            repoId, sprintId, parentId, type, title ?? string.Empty, description ?? string.Empty,
            priority, assignedToId, storyPoints, originalEstimate,
            stepsToReproduce, environment, rootCause, solution, impaction,
            unitTest, designReview, testSteps, automated), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Transitions a sprint task to a new state.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID (used for route scoping).</param>
    /// <param name="taskId">The task to transition.</param>
    /// <param name="newState">The target state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPatch("{taskId:guid}/state")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeState(Guid repoId, Guid sprintId, Guid taskId, [FromQuery] SprintTaskState newState, CancellationToken ct)
    {
        var result = await mediator.Send(new ChangeSprintTaskStateCommand(repoId, taskId, newState), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Assigns or unassigns a sprint task to a team member.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID (used for route scoping).</param>
    /// <param name="taskId">The task to assign.</param>
    /// <param name="assignedToId">The user to assign to; omit to unassign.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPatch("{taskId:guid}/assign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(
        Guid repoId, Guid sprintId, Guid taskId,
        [FromQuery] Guid? assignedToId = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new AssignSprintTaskCommand(repoId, sprintId, taskId, assignedToId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Descopes a root-level sprint task, removing it and restoring the originating backlog item to Ready.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="taskId">The root-level task to descope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPost("{taskId:guid}/descope")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Descope(Guid repoId, Guid sprintId, Guid taskId, CancellationToken ct)
    {
        var result = await mediator.Send(new DescodeSprintTaskCommand(repoId, sprintId, taskId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Updates the remaining work estimate on a task.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="taskId">The task to update.</param>
    /// <param name="hours">New remaining hours (≥ 0).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPatch("{taskId:guid}/remaining")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRemaining(
        Guid repoId, Guid sprintId, Guid taskId,
        [FromQuery] decimal hours,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new UpdateRemainingWorkCommand(repoId, sprintId, taskId, hours), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Logs completed hours and updates the remaining work estimate.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="taskId">The task to log work on.</param>
    /// <param name="hoursWorked">Hours completed in this session.</param>
    /// <param name="remainingWork">Updated remaining hours estimate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPost("{taskId:guid}/logwork")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogWork(
        Guid repoId, Guid sprintId, Guid taskId,
        [FromQuery] decimal hoursWorked,
        [FromQuery] decimal remainingWork,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new LogWorkCommand(repoId, sprintId, taskId, hoursWorked, remainingWork), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
