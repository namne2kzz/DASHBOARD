namespace DASHBOARD.Controllers.Auth.Requests;

/// <summary>HTTP request body for <c>POST /api/auth/login</c>.</summary>
/// <param name="Email">The user's registered email address.</param>
/// <param name="Password">The plaintext password.</param>
public record LoginRequest(string Email, string Password);
