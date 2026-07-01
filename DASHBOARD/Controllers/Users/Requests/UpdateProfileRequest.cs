namespace DASHBOARD.Controllers.Users.Requests;

/// <summary>HTTP request body for updating a user's display name and avatar.</summary>
/// <param name="Name">New display name.</param>
/// <param name="AvatarClass">New Tailwind CSS background class (e.g. "bg-sky-600").</param>
public sealed record UpdateProfileRequest(string Name, string AvatarClass);
