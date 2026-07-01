using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Capacity.Commands.AddDayOff;
using DASHBOARD.Application.Capacity.Commands.RemoveCapacityMember;
using DASHBOARD.Application.Capacity.Commands.RemoveDayOff;
using DASHBOARD.Application.Capacity.Commands.UpsertCapacityMember;
using DASHBOARD.Application.Capacity.DTOs;
using DASHBOARD.Application.Capacity.Queries.GetCapacity;
using DASHBOARD.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.Sprints;

/// <summary>Manages team capacity configuration and day-off entries for a sprint.</summary>
[ApiController]
[Route("api/repositories/{repoId:guid}/sprints/{sprintId:guid}/capacity")]
[Authorize]
public sealed class CapacityController(ISender mediator) : ControllerBase
{
    /// <summary>Returns all capacity members and day-off entries for the sprint.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with members and days-off.</returns>
    [HttpGet]
    [ProducesResponseType<GetCapacityResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(Guid repoId, Guid sprintId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetCapacityQuery(repoId, sprintId), ct);
        return Ok(result);
    }

    /// <summary>Creates or updates a team member's capacity for the sprint.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="role">The team role for this sprint.</param>
    /// <param name="hoursPerDay">Regular daily hours (0â€“12).</param>
    /// <param name="overtimeHoursPerDay">Overtime daily hours (0â€“6).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the <see cref="CapacityMemberDto"/>.</returns>
    [HttpPut("members/{userId:guid}")]
    [ProducesResponseType<CapacityMemberDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpsertMember(
        Guid repoId, Guid sprintId, Guid userId,
        [FromQuery] string   role,
        [FromQuery] decimal  hoursPerDay           = 8m,
        [FromQuery] decimal  overtimeHoursPerDay   = 0m,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new UpsertCapacityMemberCommand(repoId, sprintId, userId, role, hoursPerDay, overtimeHoursPerDay), ct);
        return Ok(result);
    }

    /// <summary>Removes a capacity member row from the sprint.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="capacityMemberId">The capacity row ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpDelete("members/{capacityMemberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid repoId, Guid sprintId, Guid capacityMemberId, CancellationToken ct)
    {
        var result = await mediator.Send(new RemoveCapacityMemberCommand(repoId, sprintId, capacityMemberId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Adds a day-off entry to the sprint. Date must be within the sprint's date range.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="userId">User ID, or omit for a team-wide day off.</param>
    /// <param name="date">The day-off date.</param>
    /// <param name="hours">Hours to deduct (default 8).</param>
    /// <param name="reason">Short reason description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the <see cref="DayOffDto"/>.</returns>
    [HttpPost("daysoff")]
    [ProducesResponseType<DayOffDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddDayOff(
        Guid repoId, Guid sprintId,
        [FromQuery] Guid?    userId  = null,
        [FromQuery] DateOnly date    = default,
        [FromQuery] decimal  hours   = 8m,
        [FromQuery] string   reason  = "",
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new AddDayOffCommand(repoId, sprintId, userId, date, hours, reason), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Removes a day-off entry from the sprint.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="sprintId">The sprint ID.</param>
    /// <param name="dayOffId">The day-off entry to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpDelete("daysoff/{dayOffId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveDayOff(Guid repoId, Guid sprintId, Guid dayOffId, CancellationToken ct)
    {
        var result = await mediator.Send(new RemoveDayOffCommand(repoId, sprintId, dayOffId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
