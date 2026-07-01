using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Backlog.Commands.CreateBacklogItem;
using DASHBOARD.Application.Backlog.Commands.DeleteBacklogItem;
using DASHBOARD.Application.Backlog.Commands.MoveToIteration;
using DASHBOARD.Application.Backlog.Commands.PromoteToSprint;
using DASHBOARD.Application.Backlog.Commands.RankBacklogItem;
using DASHBOARD.Application.Backlog.Commands.UpdateBacklogAcceptanceCriteria;
using DASHBOARD.Application.Backlog.Commands.UpdateBacklogDocuments;
using DASHBOARD.Application.Backlog.Commands.UpdateBacklogItem;
using DASHBOARD.Application.Backlog.Commands.UpdateBacklogTitle;
using DASHBOARD.Application.Backlog.Commands.UpdateBacklogState;
using DASHBOARD.Application.Backlog.DTOs;
using DASHBOARD.Application.Backlog.Queries.ListBacklogItems;
using DASHBOARD.Controllers.Backlog.Requests;
using DASHBOARD.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.Backlog;

/// <summary>Manages the product backlog hierarchy (Epic â†’ Feature â†’ UserStory) for a repository.</summary>
[ApiController]
[Route("api/repositories/{repoId:guid}/backlog")]
[Authorize]
public sealed class BacklogController(ISender mediator) : ControllerBase
{
    /// <summary>Returns the full backlog tree for the repository.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="type">Optional item type filter.</param>
    /// <param name="state">Optional refinement state filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with root-level items including nested children.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        Guid repoId,
        [FromQuery] BacklogItemType?  type  = null,
        [FromQuery] BacklogItemState? state = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListBacklogItemsQuery(repoId, type, state), ct);
        return Ok(result);
    }

    /// <summary>Creates a new backlog item.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="request">Item details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new <see cref="BacklogItemDto"/>.</returns>
    [HttpPost]
    [ProducesResponseType<BacklogItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(Guid repoId, [FromBody] CreateBacklogItemRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateBacklogItemCommand(
            repoId, request.Type, request.Title, request.ParentId,
            request.StoryPoints, request.TshirtSize, request.AcceptanceCriteria), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Updates an existing backlog item's fields.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="itemId">The backlog item to update.</param>
    /// <param name="request">Updated values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPut("{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid repoId, Guid itemId, [FromBody] UpdateBacklogItemRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateBacklogItemCommand(
            repoId, itemId, request.Title, request.State, request.SprintId,
            request.StoryPoints, request.TshirtSize, request.AcceptanceCriteria,
            request.Documents ?? []), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Reorders a backlog item using fractional ranking.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="itemId">The item to move.</param>
    /// <param name="request">Previous and next sibling IDs defining the new position.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPatch("{itemId:guid}/rank")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Rank(Guid repoId, Guid itemId, [FromBody] RankBacklogItemRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RankBacklogItemCommand(repoId, itemId, request.PreviousItemId, request.NextItemId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Renames a backlog item.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="itemId">The backlog item to rename.</param>
    /// <param name="request">New title.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPatch("{itemId:guid}/title")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTitle(Guid repoId, Guid itemId, [FromBody] UpdateBacklogTitleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateBacklogTitleCommand(repoId, itemId, request.Title), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Transitions a backlog item to a new refinement state.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="itemId">The backlog item to update.</param>
    /// <param name="request">Target state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPatch("{itemId:guid}/state")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateState(Guid repoId, Guid itemId, [FromBody] UpdateBacklogStateRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateBacklogStateCommand(repoId, itemId, request.State), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Updates the acceptance criteria of a backlog item.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="itemId">The backlog item to update.</param>
    /// <param name="request">New acceptance criteria text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPatch("{itemId:guid}/acceptance-criteria")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAcceptanceCriteria(Guid repoId, Guid itemId, [FromBody] UpdateBacklogAcceptanceCriteriaRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateBacklogAcceptanceCriteriaCommand(repoId, itemId, request.AcceptanceCriteria), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Replaces the document list of a backlog item.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="itemId">The backlog item to update.</param>
    /// <param name="request">New ordered document list.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPatch("{itemId:guid}/documents")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDocuments(Guid repoId, Guid itemId, [FromBody] UpdateBacklogDocumentsRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateBacklogDocumentsCommand(repoId, itemId, request.Documents), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Assigns or clears the sprint for a backlog item, transitioning its state accordingly.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="itemId">The backlog item to update.</param>
    /// <param name="request">The sprint ID to assign, or null to unschedule.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPatch("{itemId:guid}/iteration")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveToIteration(Guid repoId, Guid itemId, [FromBody] MoveToIterationRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new MoveToIterationCommand(repoId, itemId, request.SprintId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Promotes a Ready UserStory backlog item to the specified sprint.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="itemId">The backlog item to promote.</param>
    /// <param name="sprintId">The target sprint ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the new SprintTask ID.</returns>
    [HttpPost("{itemId:guid}/promote/{sprintId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PromoteToSprint(Guid repoId, Guid itemId, Guid sprintId, CancellationToken ct)
    {
        var result = await mediator.Send(new PromoteToSprintCommand(repoId, itemId, sprintId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return Ok(new { sprintTaskId = result.Value });
    }

    /// <summary>Deletes a backlog item. Fails if it has children.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="itemId">The item to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 400 when children block deletion.</returns>
    [HttpDelete("{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid repoId, Guid itemId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteBacklogItemCommand(repoId, itemId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
