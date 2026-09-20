using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Sprints.Commands.CreateSprint;
using DASHBOARD.Application.Sprints.Commands.DeleteSprint;
using DASHBOARD.Application.Sprints.Commands.UpdateSprint;
using DASHBOARD.Application.Sprints.DTOs;
using DASHBOARD.Application.Sprints.Queries.GetSprintDetail;
using DASHBOARD.Application.Sprints.Queries.GetSprintSummary;
using DASHBOARD.Application.Sprints.Queries.ListSprints;
using DASHBOARD.Controllers.Sprints.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace DASHBOARD.Controllers.Sprints;

/// <summary>Manages sprints, activation lifecycle, and capacity/velocity summaries.</summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/sprints")]
[Authorize]
public sealed class SprintsController(ISender mediator) : ControllerBase
{
    /// <summary>Returns all sprints for the repository ordered by start date descending.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the sprint list.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(Guid repoId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListSprintsQuery(repoId), ct);
        return Ok(result);
    }

    /// <summary>Returns the full sprint planning snapshot: tasks, capacity, days-off, and computed member loads.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint to load.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="SprintDetailDto"/>.</returns>
    [HttpGet("{sprintId:guid}/detail")]
    [ProducesResponseType<SprintDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(Guid repoId, Guid sprintId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSprintDetailQuery(repoId, sprintId), ct);
        return Ok(result);
    }

    /// <summary>Returns velocity and capacity metrics for a sprint.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint to summarise.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="SprintSummaryDto"/>.</returns>
    [HttpGet("{sprintId:guid}/summary")]
    [ProducesResponseType<SprintSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Summary(Guid repoId, Guid sprintId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSprintSummaryQuery(repoId, sprintId), ct);
        return Ok(result);
    }

    /// <summary>Creates a new sprint (not active by default).</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="request">Sprint name and date range.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new <see cref="SprintDto"/>.</returns>
    [HttpPost]
    [ProducesResponseType<SprintDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(Guid repoId, [FromBody] CreateSprintRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateSprintCommand(repoId, request.Name, request.StartDate, request.EndDate, request.CreateHubChannel), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Updates a sprint's name and dates.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint to update.</param>
    /// <param name="request">New name and dates.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPut("{sprintId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid repoId, Guid sprintId, [FromBody] CreateSprintRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateSprintCommand(repoId, sprintId, request.Name, request.StartDate, request.EndDate), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }


    /// <summary>Deletes a sprint. Blocked if tasks are committed.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpDelete("{sprintId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid repoId, Guid sprintId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteSprintCommand(repoId, sprintId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
