using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Auth.Commands.GoogleLogin;

/// <summary>
/// Authenticates an existing user via Google Sign-In, returning a signed JWT + refresh token pair.
/// Unlike <c>AcceptInvitationCommand</c>, this never creates a user or repository membership — it only
/// signs in an account that was already linked to a Google identity (typically via a prior invite accept).
/// </summary>
/// <param name="GoogleIdToken">The Google OAuth id_token obtained after the user signed in with Google on the frontend.</param>
public sealed record GoogleLoginCommand(string GoogleIdToken) : IRequest<Result<LoginResult>>;
