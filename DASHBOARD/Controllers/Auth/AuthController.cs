using DASHBOARD.Application.Auth.Commands.GoogleLogin;
using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Auth.Commands.Logout;
using DASHBOARD.Application.Auth.Commands.RefreshToken;
using DASHBOARD.Application.Auth.Commands.SetPassword;
using DASHBOARD.Application.Auth.DTOs;
using DASHBOARD.Application.Auth.Queries.GetCurrentUser;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Orgs.DTOs;
using DASHBOARD.Application.Orgs.Queries.GetOrganizationByAlias;
using DASHBOARD.Controllers.Auth.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Asp.Versioning;

namespace DASHBOARD.Controllers.Auth;

/// <summary>Handles authentication: login, token refresh, logout, and current-user profile.</summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(
    ISender mediator,
    ICurrentUserService currentUser,
    AuthCookieWriter cookies) : ControllerBase
{
    /// <summary>Rate-limit policy guarding the anonymous alias lookup; configured in Program.cs.</summary>
    public const string OrgLookupRateLimitPolicy = "org-lookup";

    /// <summary>Resolves a tenant alias typed on the first step of sign-in.</summary>
    /// <remarks>
    /// Anonymous by necessity — the user cannot authenticate before naming their organisation. A
    /// 200 therefore tells the caller that tenant exists, which is why the route is rate limited
    /// well below the global allowance and the body carries the alias and nothing else.
    /// </remarks>
    /// <param name="alias">The URL-safe tenant alias.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="OrganizationLookupDto"/>, 404 when no organisation has that alias.</returns>
    [HttpGet("orgs/{alias}")]
    [AllowAnonymous]
    [EnableRateLimiting(OrgLookupRateLimitPolicy)]
    [ProducesResponseType<OrganizationLookupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetOrganization(string alias, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrganizationByAliasQuery(alias), ct);
        if (result.IsFailure) return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

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
        // Set the HttpOnly auth cookies. The body is still returned so the SPA can bootstrap its
        // profile signals and the HUB SSO handoff can read the tokens for its cross-origin redirect.
        cookies.WriteSession(Response, result.Value!);
        return Ok(result.Value);
    }

    /// <summary>
    /// Redeems a one-time password-setup link so a newly provisioned user can choose their first
    /// password, then signs them in.
    /// </summary>
    /// <remarks>
    /// Issued when NKey provisions an organisation: the first admin account is created without a
    /// password and this link is emailed instead. Holding the token proves control of that mailbox,
    /// so no further credentials are required.
    /// </remarks>
    /// <param name="request">The setup token and the chosen password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="LoginResult"/>, 400 when the link is invalid, expired or already used, 422 on a weak password.</returns>
    [HttpPost("set-password")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SetPasswordCommand(request.Token, request.NewPassword), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        cookies.WriteSession(Response, result.Value!);
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
        cookies.WriteSession(Response, result.Value!);
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
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest? request, CancellationToken ct)
    {
        // Prefer the body (HUB SSO / non-browser callers) but fall back to the HttpOnly cookie,
        // which is how the SPA refreshes now that it no longer holds the token in JS.
        var token = request?.RefreshToken;
        if (string.IsNullOrEmpty(token))
            token = Request.Cookies[DASHBOARD.Core.Constants.AppConstants.RefreshTokenCookie];

        if (string.IsNullOrEmpty(token))
        {
            // Deliberately not clearing the cookies here. This branch means only that no refresh
            // token reached us — a caller that never had one, or whose refresh cookie expired
            // while the access cookie is still perfectly valid. Expiring the access cookie on the
            // way out would end a working session because of a request that failed to start one,
            // which is how a single stray refresh call used to sign people out.
            return Unauthorized(new { error = "No refresh token supplied." });
        }

        var result = await mediator.Send(new RefreshTokenCommand(token), ct);
        // Rotation issued a new pair — refresh the cookies so the next request carries them.
        cookies.WriteSession(Response, result);
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
        cookies.ClearSession(Response);
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
