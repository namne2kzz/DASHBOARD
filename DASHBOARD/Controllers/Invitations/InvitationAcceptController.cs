using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Invitations.Commands.AcceptInvitation;
using DASHBOARD.Controllers.Invitations.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DASHBOARD.Controllers.Invitations;

/// <summary>Accepts a repository invitation for an unauthenticated, externally-invited user.</summary>
[ApiController]
[Route("api/invitations")]
[AllowAnonymous]
public sealed class InvitationAcceptController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Verifies the one-time invite token and a Google id_token, creates or reuses the user account,
    /// adds them to the repository, and returns a signed-in session (invited users are Google-only
    /// and have no password to use the regular login endpoint with).
    /// </summary>
    /// <param name="request">The raw invite token and the Google id_token from the frontend sign-in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="LoginResult"/>, or 400 with a descriptive error (invalid/expired/mismatched).</returns>
    [HttpPost("accept")]
    [EnableRateLimiting("accept-invitation")]
    [ProducesResponseType<LoginResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AcceptInvitationCommand(request.RawToken, request.GoogleIdToken), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }
}
