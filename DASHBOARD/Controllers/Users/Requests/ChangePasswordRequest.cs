namespace DASHBOARD.Controllers.Users.Requests;

/// <summary>HTTP request body for changing the authenticated user's password.</summary>
/// <param name="OldPassword">Current password for identity verification.</param>
/// <param name="NewPassword">The desired replacement password.</param>
public sealed record ChangePasswordRequest(string OldPassword, string NewPassword);
