namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>Claims extracted from a verified Google id_token.</summary>
/// <param name="Subject">Google's permanent unique user identifier (<c>sub</c> claim). Use this — not email — for lookups.</param>
/// <param name="Email">Google account email address.</param>
/// <param name="Name">User's display name from their Google profile.</param>
/// <param name="PictureUrl">Avatar URL from the Google profile, if available.</param>
public sealed record GoogleUserInfo(string Subject, string Email, string? Name, string? PictureUrl);

/// <summary>Abstraction for verifying Google id_tokens. Implementation lives in Infrastructure (Google.Apis.Auth).</summary>
public interface IGoogleAuthService
{
    /// <summary>Validates a Google id_token and extracts the user's profile claims.</summary>
    /// <param name="idToken">The id_token string returned by the Google OAuth flow on the frontend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Verified user claims from the Google token.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the token is invalid or cannot be verified.</exception>
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct);
}
