using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Commands.ConfirmAvatarUpload;

/// <summary>
/// Handles <see cref="ConfirmAvatarUploadCommand"/>:
/// <list type="number">
///   <item>Rejects any object key that does not belong to the current user (path-traversal guard).</item>
///   <item>Verifies the object actually exists in MinIO — the browser cannot fake a successful upload.</item>
///   <item>Deletes the previous avatar file from storage (if any) to avoid orphaned objects.</item>
///   <item>Upserts the public URL into <c>UserSetting</c> under key <see cref="UserSettingKeys.AvatarUrl"/>.</item>
/// </list>
/// </summary>
public sealed class ConfirmAvatarUploadCommandHandler(
    IStorageService       storage,
    IAppSettings          settings,
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<ConfirmAvatarUploadCommand, Result<string>>
{
    /// <summary>Validates, verifies, and persists the avatar URL after a successful browser upload.</summary>
    /// <param name="command">Contains the object key returned by the initiate step.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The public avatar URL on success, or a failure result.</returns>
    public async Task<Result<string>> Handle(ConfirmAvatarUploadCommand command, CancellationToken ct)
    {
        var userId = user.UserId;

        // ── 1. Path-traversal guard — objectKey must belong to this user ─────
        var expectedPrefix = $"avatars/{userId}/";
        if (!command.ObjectKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
            return Result<string>.Failure("Object key does not belong to the current user.");

        // ── 2. Verify object exists in MinIO (browser cannot fake this) ──────
        var bucket = settings.MinioAvatarBucket;
        var exists = await ObjectExistsAsync(bucket, command.ObjectKey, ct);
        if (!exists)
            return Result<string>.Failure("Avatar file was not found in storage. Please upload again.");

        // ── 3. Delete previous avatar if one exists ──────────────────────────
        var existing = await db.Set<UserSetting>()
            .AsTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Key == UserSettingKeys.AvatarUrl, ct);

        if (existing is not null && !string.IsNullOrEmpty(existing.Value))
        {
            var oldKey = ExtractObjectKey(existing.Value, bucket);
            if (oldKey is not null && oldKey != command.ObjectKey)
                await storage.DeleteAsync(bucket, oldKey, ct);
        }

        // ── 4. Upsert public URL into UserSetting ────────────────────────────
        var publicUrl = storage.GetPublicUrl(bucket, command.ObjectKey);

        if (existing is not null)
            existing.SetValue(publicUrl);
        else
            db.Set<UserSetting>().Add(UserSetting.Create(userId, UserSettingKeys.AvatarUrl, publicUrl));

        await uow.CommitAsync(ct);
        return Result<string>.Success(publicUrl);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks whether <paramref name="objectKey"/> exists in <paramref name="bucket"/> by issuing
    /// a lightweight HeadObject request. Returns <c>false</c> on 404; rethrows other errors.
    /// </summary>
    private async Task<bool> ObjectExistsAsync(string bucket, string objectKey, CancellationToken ct)
    {
        try
        {
            // HeadObject fetches only metadata — zero data transfer, cheapest existence check.
            await storage.HeadObjectAsync(bucket, objectKey, ct);
            return true;
        }
        catch (StorageObjectNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts the object key from a full public URL by stripping the base URL and bucket prefix.
    /// Returns <c>null</c> if the URL does not match the expected pattern.
    /// </summary>
    private string? ExtractObjectKey(string publicUrl, string bucket)
    {
        var prefix = $"{settings.MinioPublicBaseUrl.TrimEnd('/')}/{bucket}/";
        return publicUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? publicUrl[prefix.Length..]
            : null;
    }
}
