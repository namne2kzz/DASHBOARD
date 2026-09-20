using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Users.Commands.RequestAvatarUploadUrl;

/// <summary>Requests a presigned PUT URL so the browser can upload an avatar image directly to object storage.</summary>
public sealed record RequestAvatarUploadUrlCommand : IRequest<Result<AvatarUploadUrlResult>>;

/// <summary>Returned by <see cref="RequestAvatarUploadUrlCommand"/> on success.</summary>
/// <param name="UploadUrl">Presigned PUT URL — valid for 5 minutes. The browser PUTs the file body directly to this URL.</param>
/// <param name="ObjectKey">The object key assigned by the server. Must be sent back in the confirm step.</param>
public sealed record AvatarUploadUrlResult(string UploadUrl, string ObjectKey);
