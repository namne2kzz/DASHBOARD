namespace DASHBOARD.Controllers.Invitations.Requests;

/// <summary>HTTP request body for accepting a repository invitation.</summary>
/// <param name="RawToken">The one-time token from the invite link's URL fragment.</param>
/// <param name="GoogleIdToken">The Google OAuth id_token obtained after signing in with Google on the frontend.</param>
public sealed record AcceptInvitationRequest(string RawToken, string GoogleIdToken);
