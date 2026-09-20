using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Users.Commands.ConfirmAvatarUpload;

/// <summary>
/// Confirms that the browser has finished uploading an avatar to object storage and persists
/// the resulting public URL in the user's settings.
/// The server verifies the object actually exists before saving — the browser cannot fake this.
/// </summary>
/// <param name="ObjectKey">
/// The object key returned by <c>RequestAvatarUploadUrlCommand</c>.
/// Must match the pattern <c>avatars/{userId}/...</c> — validated server-side.
/// </param>
public sealed record ConfirmAvatarUploadCommand(string ObjectKey) : IRequest<Result<string>>;
