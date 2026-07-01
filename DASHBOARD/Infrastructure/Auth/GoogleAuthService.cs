using DASHBOARD.Application.Common.Interfaces;
using Google.Apis.Auth;

namespace DASHBOARD.Infrastructure.Auth;

/// <summary>Verifies Google id_tokens using Google.Apis.Auth and returns extracted profile claims.</summary>
internal sealed class GoogleAuthService(IAppSettings settings) : IGoogleAuthService
{
    /// <summary>Validates the Google id_token against the configured ClientId and extracts user claims.</summary>
    /// <param name="idToken">The id_token string from the frontend Google OAuth flow.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Verified Google user claims.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the token is invalid or the audience does not match.</exception>
    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct)
    {
        var validationSettings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [settings.GoogleClientId],
        };

        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);

        return new GoogleUserInfo(
            Subject:    payload.Subject,
            Email:      payload.Email,
            Name:       payload.Name,
            PictureUrl: payload.Picture);
    }
}
