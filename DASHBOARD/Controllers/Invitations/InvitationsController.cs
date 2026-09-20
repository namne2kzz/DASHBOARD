using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Invitations.Commands.CreateInvitation;
using DASHBOARD.Application.Invitations.Commands.RevokeInvitation;
using DASHBOARD.Application.Invitations.DTOs;
using DASHBOARD.Application.Invitations.Queries.ListInvitations;
using DASHBOARD.Controllers.Invitations.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace DASHBOARD.Controllers.Invitations;

/// <summary>Issues and manages repository invitations to external emails.</summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/invitations")]
[Authorize]
public sealed class InvitationsController(ISender mediator) : ControllerBase
{
    /// <summary>Invites an external email to join the repository. Revokes any prior pending invite for the same email.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="request">The invitee's email address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the created <see cref="InvitationDto"/>.</returns>
    [HttpPost]
    [ProducesResponseType<InvitationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(Guid repoId, [FromBody] CreateInvitationRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateInvitationCommand(repoId, request.Email, request.DefaultRole, request.RoleId, request.ManagerId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Returns every invitation ever sent for the repository, newest first. Requires the InviteMembers permission.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the invitation list.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InvitationListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(Guid repoId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListInvitationsQuery(repoId), ct);
        return Ok(result);
    }

    /// <summary>Revokes a still-pending invitation, e.g. one sent by mistake or no longer needed.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="invitationId">The invitation to revoke.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 400 when the invitation is not Pending.</returns>
    [HttpPost("{invitationId:guid}/revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid repoId, Guid invitationId, CancellationToken ct)
    {
        var result = await mediator.Send(new RevokeInvitationCommand(repoId, invitationId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
