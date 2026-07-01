namespace DASHBOARD.Controllers.Auth.Requests;

/// <summary>HTTP request body for <c>POST /api/auth/refresh</c>.</summary>
/// <param name="RefreshToken">The plaintext refresh token received from a previous login or refresh response.</param>
public record RefreshTokenRequest(string RefreshToken);
