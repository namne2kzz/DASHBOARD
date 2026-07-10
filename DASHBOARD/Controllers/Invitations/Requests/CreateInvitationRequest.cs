namespace DASHBOARD.Controllers.Invitations.Requests;

/// <summary>HTTP request body for inviting an external email to a repository.</summary>
/// <param name="Email">Email address to invite. Must not already exist in the system.</param>
public sealed record CreateInvitationRequest(string Email);
