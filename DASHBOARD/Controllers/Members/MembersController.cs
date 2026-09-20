using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Members.Commands.AddMember;
using DASHBOARD.Application.Members.Commands.RemoveMember;
using DASHBOARD.Application.Members.Commands.UpdateMemberRole;
using DASHBOARD.Application.Members.DTOs;
using DASHBOARD.Application.Members.Queries.ListMembers;
using DASHBOARD.Controllers.Members.Requests;
using DASHBOARD.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace DASHBOARD.Controllers.Members;

/// <summary>Manages membership and role assignments within a repository.</summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/members")]
[Authorize]
public sealed class MembersController(ISender mediator) : ControllerBase
{
    /// <summary>Returns all members of the repository, optionally filtered by team role.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="role">Optional role filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the member list.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(Guid repoId, [FromQuery] string? role, CancellationToken ct)
    {
        var result = await mediator.Send(new ListMembersQuery(repoId, role), ct);
        return Ok(result);
    }

    /// <summary>Adds an existing user to the repository with the specified role.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="request">User ID and role details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new <see cref="MemberDto"/>.</returns>
    [HttpPost]
    [ProducesResponseType<MemberDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Add(Guid repoId, [FromBody] AddMemberRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AddMemberCommand(repoId, request.UserId, request.DefaultRole, request.RoleId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Updates the role assignment of an existing member.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="memberId">The membership row to update.</param>
    /// <param name="request">New role details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 400 when business rules prevent the change.</returns>
    [HttpPut("{memberId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRole(Guid repoId, Guid memberId, [FromBody] UpdateMemberRoleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateMemberRoleCommand(repoId, memberId, request.DefaultRole, request.RoleId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Removes a member from the repository. Fails if they are the last ScrumMaster.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="memberId">The membership row to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 400 when the last ScrumMaster would be removed.</returns>
    [HttpDelete("{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid repoId, Guid memberId, CancellationToken ct)
    {
        var result = await mediator.Send(new RemoveMemberCommand(repoId, memberId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
