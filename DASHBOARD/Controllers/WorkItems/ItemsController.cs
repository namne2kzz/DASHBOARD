using DASHBOARD.Application.SprintTasks.Commands.ChangeSprintTaskState;
using DASHBOARD.Application.SprintTasks.Commands.CreateSprintTask;
using DASHBOARD.Application.SprintTasks.Commands.DeleteSprintTask;
using DASHBOARD.Application.SprintTasks.Commands.UpdateSprintTask;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Application.SprintTasks.Queries.ListStandaloneItems;
using DASHBOARD.Application.SprintTasks.Queries.SearchParentStories;
using DASHBOARD.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace DASHBOARD.Controllers.WorkItems;

/// <summary>Manages work items (Bug, TestPlan, Task) independently of sprints.</summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/items")]
[Authorize]
public sealed class ItemsController(ISender mediator) : ControllerBase
{
    /// <summary>Returns standalone work items not yet assigned to a sprint.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="type">Optional type filter.</param>
    /// <param name="state">Optional state filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the list of <see cref="SprintTaskSummaryDto"/>.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        Guid repoId,
        [FromQuery] SprintTaskType?  type  = null,
        [FromQuery] SprintTaskState? state = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListStandaloneItemsQuery(repoId, type, state), ct);
        return Ok(result);
    }

    /// <summary>Searches User Story items in the repository for the create-work-item "Parent" picker.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="q">Optional title or work item number fragment to match.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with up to 20 matching <see cref="WorkItemPickerDto"/>.</returns>
    [HttpGet("picker")]
    [ProducesResponseType<IReadOnlyList<WorkItemPickerDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SearchPicker(Guid repoId, [FromQuery] string? q, CancellationToken ct)
    {
        var result = await mediator.Send(new SearchParentStoriesQuery(repoId, q), ct);
        return Ok(result);
    }

    /// <summary>Creates a standalone work item (Bug or TestPlan) not assigned to a sprint.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="request">Work item creation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new <see cref="SprintTaskDto"/>.</returns>
    [HttpPost]
    [ProducesResponseType<SprintTaskDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(Guid repoId, [FromBody] CreateItemRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateSprintTaskCommand(
            repoId, null, null,
            request.Type, request.Title, request.Description ?? string.Empty,
            request.Priority, request.AssignedToId,
            0, request.OriginalEstimate), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Updates a work item's fields.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The work item to update.</param>
    /// <param name="request">Updated field values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPut("{taskId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid repoId, Guid taskId, [FromBody] UpdateItemRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateSprintTaskCommand(
            repoId, taskId,
            request.Title, request.Description ?? string.Empty, request.Priority,
            request.AssignedToId, 0, request.OriginalEstimate,
            request.StepsToReproduce, request.Environment, request.RootCause,
            request.Solution, request.Impaction, request.UnitTest, request.DesignReview,
            request.TestSteps, request.Automated, null, null, 0), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Transitions a work item to a new state.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The work item to transition.</param>
    /// <param name="newState">The target state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPatch("{taskId:guid}/state")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeState(Guid repoId, Guid taskId, [FromQuery] SprintTaskState newState, CancellationToken ct)
    {
        var result = await mediator.Send(new ChangeSprintTaskStateCommand(repoId, taskId, newState), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Soft-deletes a work item.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The work item to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpDelete("{taskId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid repoId, Guid taskId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteSprintTaskCommand(repoId, taskId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}

/// <summary>Request body for creating a standalone work item.</summary>
public sealed record CreateItemRequest(
    SprintTaskType   Type,
    string           Title,
    string?          Description,
    WorkItemPriority Priority,
    Guid?            AssignedToId,
    decimal          OriginalEstimate);

/// <summary>Request body for updating a work item.</summary>
public sealed record UpdateItemRequest(
    string           Title,
    string?          Description,
    WorkItemPriority Priority,
    Guid?            AssignedToId,
    decimal          OriginalEstimate,
    string?          StepsToReproduce,
    string?          Environment,
    string?          RootCause,
    string?          Solution,
    string?          Impaction,
    string?          UnitTest,
    string?          DesignReview,
    List<string>?    TestSteps,
    bool?            Automated);
