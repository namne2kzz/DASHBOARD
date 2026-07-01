using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Invitations.Commands.AcceptInvitation;

/// <summary>Accepts a repository invitation by verifying the one-time token and a Google id_token for identity.</summary>
/// <param name="RawToken">The one-time token from the invite link query string.</param>
/// <param name="GoogleIdToken">The Google OAuth id_token obtained after the user signed in with Google on the frontend.</param>
public sealed record AcceptInvitationCommand(
    string RawToken,
    string GoogleIdToken) : IRequest<Result<Guid>>;
