namespace DASHBOARD.Controllers.Users.Requests;

/// <summary>HTTP request body for confirming a completed avatar upload.</summary>
/// <param name="ObjectKey">The object key returned by the upload-url endpoint.</param>
public sealed record ConfirmAvatarUploadRequest(string ObjectKey);
