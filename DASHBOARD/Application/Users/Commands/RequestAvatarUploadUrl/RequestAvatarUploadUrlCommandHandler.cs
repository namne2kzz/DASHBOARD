using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Users.Commands.RequestAvatarUploadUrl;

/// <summary>
/// Handles <see cref="RequestAvatarUploadUrlCommand"/>: generates a server-controlled object key
/// and a presigned PUT URL so the browser can upload directly to MinIO without routing the binary
/// through the API server.
/// </summary>
public sealed class RequestAvatarUploadUrlCommandHandler(
    IStorageService    storage,
    IAppSettings       settings,
    IRequestUserContext user) : IRequestHandler<RequestAvatarUploadUrlCommand, Result<AvatarUploadUrlResult>>
{
    private static readonly TimeSpan UploadUrlTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Generates a unique object key scoped to the current user and returns a presigned PUT URL.
    /// The object key format is <c>avatars/{userId}/{unixMs}.jpg</c> — the timestamp suffix
    /// ensures cache-busting when the user replaces their avatar.
    /// </summary>
    /// <param name="command">The command (no fields — caller identity comes from <see cref="IRequestUserContext"/>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Presigned URL + object key on success.</returns>
    public async Task<Result<AvatarUploadUrlResult>> Handle(
        RequestAvatarUploadUrlCommand command, CancellationToken ct)
    {
        var userId    = user.UserId;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var objectKey = $"avatars/{userId}/{timestamp}.jpg";

        var uploadUrl = await storage.GenerateUploadUrlAsync(
            bucket:    settings.MinioAvatarBucket,
            objectKey: objectKey,
            expiry:    UploadUrlTtl,
            ct:        ct);

        return Result<AvatarUploadUrlResult>.Success(new AvatarUploadUrlResult(uploadUrl, objectKey));
    }
}
