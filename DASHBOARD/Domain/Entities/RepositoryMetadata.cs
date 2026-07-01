using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;

namespace DASHBOARD.Domain.Entities;

/// <summary>
/// A repository-level metadata value entry.
/// Acts as a per-key catalog: admins populate the available options
/// (e.g. all released versions for <see cref="MetadataKey.FixedInVersion"/>)
/// and users pick from this list when editing a work item.
/// </summary>
public sealed class RepositoryMetadata : Common.BaseEntity, ISoftDelete
{
    /// <summary>Gets or sets the repository this metadata entry belongs to. Null when <see cref="IsGlobal"/> is true.</summary>
    public Guid? RepositoryId { get; set; }

    /// <summary>Gets or sets a value indicating whether this entry is shared across all repositories (global catalog value).</summary>
    public bool IsGlobal { get; set; }

    /// <summary>Gets or sets the well-known metadata key (e.g. FixedInVersion, ImplementedInBuild).</summary>
    public MetadataKey Key { get; set; }

    /// <summary>Gets or sets the selectable value for this key (e.g. "1.4.1", "build-321").</summary>
    public string Value { get; set; } = string.Empty;

    // ── ISoftDelete ──────────────────────────────────────────────────────────
    /// <inheritdoc/>
    public bool IsDeleted { get; set; }

    /// <inheritdoc/>
    public DateTime? DeletedAt { get; set; }

    /// <inheritdoc/>
    public Guid? DeletedByUserId { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    /// <summary>The owning repository.</summary>
    public Repository? Repository { get; set; }
}
