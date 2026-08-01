namespace DASHBOARD.Controllers.Invitations.Requests;

/// <summary>HTTP request body for inviting an external email to a repository.</summary>
/// <param name="Email">Email address to invite. Must not already exist in the system.</param>
/// <param name="DefaultRole">The team role (discipline) the invitee will be assigned on acceptance.</param>
/// <param name="RoleId">The role (default or custom) granting permissions, applied on acceptance. Required.</param>
/// <param name="ManagerId">Optional manager the invitee's account will report to, applied on acceptance.</param>
public sealed record CreateInvitationRequest(string Email, string DefaultRole, Guid RoleId, Guid? ManagerId = null);
