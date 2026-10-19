namespace DASHBOARD.Application.Common.Helpers;

/// <summary>Allowlist and size limits for work-item attachments and pasted inline images.</summary>
public static class AttachmentPolicy
{
    /// <summary>Maximum size for a pasted inline image (10 MB).</summary>
    public const long MaxImageBytes = 10L * 1024 * 1024;

    /// <summary>Maximum size for a general file attachment (50 MB).</summary>
    public const long MaxAttachmentBytes = 50L * 1024 * 1024;

    /// <summary>Content types accepted as inline images.</summary>
    public static readonly HashSet<string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp",
    };

    /// <summary>Content types accepted as general attachments (includes images).</summary>
    public static readonly HashSet<string> AttachmentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        "image/png", "image/jpeg", "image/gif", "image/webp", "image/svg+xml",
        // Documents
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        // Text / data
        "text/plain", "text/csv", "text/markdown",
        "application/json", "application/xml", "text/xml",
        // Archives
        "application/zip", "application/x-zip-compressed",
    };

    /// <summary>Returns true when a content type is an allowed inline image within the size limit.</summary>
    /// <param name="contentType">The file's MIME type.</param>
    /// <param name="sizeBytes">The file size in bytes.</param>
    public static bool IsValidImage(string contentType, long sizeBytes)
        => sizeBytes > 0 && sizeBytes <= MaxImageBytes && ImageContentTypes.Contains(contentType);

    /// <summary>Returns true when a content type is an allowed attachment within the size limit.</summary>
    /// <param name="contentType">The file's MIME type.</param>
    /// <param name="sizeBytes">The file size in bytes.</param>
    public static bool IsValidAttachment(string contentType, long sizeBytes)
        => sizeBytes > 0 && sizeBytes <= MaxAttachmentBytes && AttachmentContentTypes.Contains(contentType);

    /// <summary>Derives a safe file extension from a content type for object-key naming.</summary>
    /// <param name="contentType">The file's MIME type.</param>
    /// <returns>Extension including the leading dot, or empty string if unknown.</returns>
    public static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png"  => ".png",
        "image/jpeg" => ".jpg",
        "image/gif"  => ".gif",
        "image/webp" => ".webp",
        "image/svg+xml" => ".svg",
        "application/pdf" => ".pdf",
        _ => string.Empty,
    };
}
