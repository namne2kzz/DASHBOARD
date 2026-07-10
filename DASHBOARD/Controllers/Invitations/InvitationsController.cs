using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Invitations.Commands.CreateInvitation;
using DASHBOARD.Application.Invitations.DTOs;
using DASHBOARD.Controllers.Invitations.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.Invitations;

/// <summary>Issues repository invitations to external emails.</summary>
[ApiController]
[Route("api/repositories/{repoId:guid}/invitations")]
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
        var result = await mediator.Send(new CreateInvitationCommand(repoId, request.Email), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }
}
