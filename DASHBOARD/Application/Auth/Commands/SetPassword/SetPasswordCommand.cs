using DASHBOARD.Application.Auth.Commands.Login;
using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Auth.Commands.SetPassword;

/// <summary>
/// Redeems a one-time password-setup token to choose a first password, then signs the user in.
/// </summary>
/// <remarks>
/// Used by an organisation's first admin after NKey provisions their tenant. Holding the token is
/// what proves the person controls the mailbox it was sent to, so no further authentication is
/// required — the same reasoning as accepting an invitation.
/// </remarks>
/// <param name="RawToken">The one-time token taken from the setup link's URL fragment.</param>
/// <param name="NewPassword">The password the user has chosen.</param>
public sealed record SetPasswordCommand(
    string RawToken,
    string NewPassword) : IRequest<Result<LoginResult>>;
