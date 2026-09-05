using DASHBOARD.Application.Auth.Commands.GoogleLogin;
using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Auth.Commands.Logout;
using DASHBOARD.Application.Auth.Commands.RefreshToken;
using DASHBOARD.Application.Auth.DTOs;
using DASHBOARD.Application.Auth.Queries.GetCurrentUser;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Controllers.Auth.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.Auth;

/// <summary>Handles authentication: login, token refresh, logout, and current-user profile.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Authenticates a user and returns an access + refresh token pair.</summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="LoginResult"/>, 401 on invalid credentials, 422 on validation failure.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new LoginCommand(request.OrgAlias, request.Email, request.Password), ct);
        if (result.IsFailure) return Unauthorized(new { error = result.Error });
        return Ok(result.Value);
    }

    /// <summary>
    /// Authenticates an existing user via Google Sign-In. Never creates an account or repository
    /// membership — only signs in an account already linked to that Google identity (typically via a
    /// prior invite accept). Use the invite flow to onboard a brand-new user.
    /// </summary>
    /// <param name="request">The Google id_token from the frontend sign-in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="LoginResult"/>, or 401 when no account is linked to that Google identity.</returns>
    [HttpPost("google-login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new GoogleLoginCommand(request.GoogleIdToken), ct);
        if (result.IsFailure) return Unauthorized(new { error = result.Error });
        return Ok(result.Value);
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new access + refresh token pair (token rotation).
    /// The submitted refresh token is immediately revoked on success.
    /// </summary>
    /// <param name="request">The plaintext refresh token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with a new <see cref="LoginResult"/>, or 401 when the token is invalid/expired.</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RefreshTokenCommand(request.RefreshToken), ct);
        return Ok(result);
    }

    /// <summary>Revokes both the current access token and its paired refresh token server-side.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 on success. The client must also clear tokens from local storage.</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (currentUser.JwtId is null || currentUser.UserId is null)
            return Unauthorized();

        await mediator.Send(new LogoutCommand(currentUser.JwtId, currentUser.UserId.Value), ct);
        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>Returns the full profile of the currently authenticated user.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="UserDto"/>, or 401 if not authenticated.</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await mediator.Send(new GetCurrentUserQuery(), ct);
        return Ok(result);
    }
}
