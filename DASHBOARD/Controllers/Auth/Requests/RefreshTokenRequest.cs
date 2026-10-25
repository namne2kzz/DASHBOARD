namespace DASHBOARD.Controllers.Auth.Requests;

/// <summary>HTTP request body for <c>POST /api/auth/refresh</c>.</summary>
/// <param name="RefreshToken">
/// The plaintext refresh token. Optional: the SPA leaves it null and relies on the HttpOnly
/// refresh cookie, which the browser sends automatically; only non-cookie callers populate it.
/// </param>
public record RefreshTokenRequest(string? RefreshToken = null);
