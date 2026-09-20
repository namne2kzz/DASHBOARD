using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.SmartBoard.Commands.CreateColumn;
using DASHBOARD.Application.SmartBoard.Commands.ReorderColumns;
using DASHBOARD.Application.SmartBoard.Commands.UpdateColumn;
using DASHBOARD.Application.SmartBoard.DTOs;
using DASHBOARD.Application.SmartBoard.Queries.GetBoard;
using DASHBOARD.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace DASHBOARD.Controllers.SmartBoard;

/// <summary>Manages the WIP-limited kanban board columns.</summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/board")]
[Authorize]
public sealed class SmartBoardController(ISender mediator) : ControllerBase
{
    /// <summary>Returns all board columns in display order.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the ordered column list.</returns>
    [HttpGet("columns")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetColumns(Guid repoId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetBoardQuery(repoId), ct);
        return Ok(result);
    }

    /// <summary>Creates a new board column.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="name">Column display name.</param>
    /// <param name="mappedState">The sprint-task state whose items appear in this column.</param>
    /// <param name="wipLimit">Max items allowed (0 = unlimited).</param>
    /// <param name="wipMode">Soft (warn) or Hard (block).</param>
    /// <param name="agingLimitDays">Days before an item is flagged as aging.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new <see cref="SmartBoardColumnDto"/>.</returns>
    [HttpPost("columns")]
    [ProducesResponseType<SmartBoardColumnDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateColumn(
        Guid repoId,
        [FromQuery] string          name,
        [FromQuery] SprintTaskState mappedState,
        [FromQuery] int             wipLimit       = 0,
        [FromQuery] WipMode         wipMode        = WipMode.Soft,
        [FromQuery] int             agingLimitDays = 5,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new CreateColumnCommand(repoId, name, mappedState, wipLimit, wipMode, agingLimitDays), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Updates an existing board column's configuration and state mapping.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="columnId">The column to update.</param>
    /// <param name="name">New display name.</param>
    /// <param name="mappedState">New sprint-task state mapping.</param>
    /// <param name="wipLimit">New WIP limit.</param>
    /// <param name="wipMode">New WIP mode.</param>
    /// <param name="agingLimitDays">New aging limit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPut("columns/{columnId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateColumn(
        Guid repoId, Guid columnId,
        [FromQuery] string          name,
        [FromQuery] SprintTaskState mappedState,
        [FromQuery] int             wipLimit,
        [FromQuery] WipMode         wipMode,
        [FromQuery] int             agingLimitDays,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new UpdateColumnCommand(repoId, columnId, name, mappedState, wipLimit, wipMode, agingLimitDays), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Reorders all board columns by assigning new positions based on the supplied ordered ID list.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="orderedColumnIds">All column IDs in the desired display order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPut("columns/order")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReorderColumns(Guid repoId, [FromBody] List<Guid> orderedColumnIds, CancellationToken ct)
    {
        var result = await mediator.Send(new ReorderColumnsCommand(repoId, orderedColumnIds), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
