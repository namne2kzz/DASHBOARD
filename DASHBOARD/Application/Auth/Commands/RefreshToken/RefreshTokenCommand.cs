using DASHBOARD.Application.Auth.Commands.Login;
using MediatR;

namespace DASHBOARD.Application.Auth.Commands.RefreshToken;

/// <summary>
/// Exchanges a valid refresh token for a new access + refresh token pair (token rotation).
/// The supplied refresh token is revoked after a successful exchange.
/// </summary>
/// <param name="RefreshToken">The plaintext refresh token received from a previous login or refresh.</param>
public record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResult>;
