namespace DASHBOARD.Controllers.Auth.Requests;

/// <summary>HTTP request body for <c>POST /api/auth/google-login</c>.</summary>
/// <param name="GoogleIdToken">The Google id_token obtained after the user signed in with Google on the frontend.</param>
public sealed record GoogleLoginRequest(string GoogleIdToken);
