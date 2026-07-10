using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Invitations.Commands.AcceptInvitation;

/// <summary>
/// Accepts a repository invitation by verifying the one-time token and a Google id_token for identity,
/// then issues a signed-in session — invited users are Google-only and cannot use the password login flow.
/// </summary>
/// <param name="RawToken">The one-time token from the invite link query string.</param>
/// <param name="GoogleIdToken">The Google OAuth id_token obtained after the user signed in with Google on the frontend.</param>
public sealed record AcceptInvitationCommand(
    string RawToken,
    string GoogleIdToken) : IRequest<Result<LoginResult>>;
