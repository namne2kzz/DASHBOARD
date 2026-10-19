namespace DASHBOARD.Domain.Entities;

/// <summary>A file attached to a <see cref="SprintTask"/> (work item), stored in object storage.</summary>
public sealed class WorkItemAttachment : Common.BaseEntity
{
    /// <summary>Gets or sets the work item this attachment belongs to.</summary>
    public Guid SprintTaskId { get; set; }

    /// <summary>Gets or sets the repository (denormalized for query isolation and permission checks).</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the user who uploaded the file.</summary>
    public Guid UploadedById { get; set; }

    /// <summary>Gets or sets the original file name shown to users.</summary>
    public string FileName { get; set; } = default!;

    /// <summary>Gets or sets the MIME content type.</summary>
    public string ContentType { get; set; } = default!;

    /// <summary>Gets or sets the file size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Gets or sets the object storage key (bucket-relative path).</summary>
    public string ObjectKey { get; set; } = default!;

    /// <summary>Gets or sets the public URL used to download the file.</summary>
    public string PublicUrl { get; set; } = default!;

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Parent work item.</summary>
    public SprintTask? SprintTask { get; set; }

    /// <summary>The uploading user.</summary>
    public User? UploadedBy { get; set; }
}
