namespace DASHBOARD.Controllers.Roles.Requests;

/// <summary>HTTP request body for cloning a role.</summary>
/// <param name="NewName">Name for the cloned role.</param>
public sealed record CloneRoleRequest(string NewName);
