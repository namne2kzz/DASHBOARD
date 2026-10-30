namespace DASHBOARD.Controllers.Auth.Requests;

/// <summary>Payload for redeeming a password-setup link.</summary>
/// <param name="Token">
/// The one-time token taken from the setup link's URL fragment. The frontend reads it from
/// <c>location.hash</c> and posts it here, so it never appears in a request URL.
/// </param>
/// <param name="NewPassword">The password the user has chosen.</param>
public sealed record SetPasswordRequest(string Token, string NewPassword);
