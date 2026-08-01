namespace DASHBOARD.Controllers.Users.Requests;

/// <summary>HTTP request body for setting a user's manager.</summary>
/// <param name="ManagerId">The new manager's ID, or null to clear the assignment (make the user a root).</param>
public sealed record SetUserManagerRequest(Guid? ManagerId);
