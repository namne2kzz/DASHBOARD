namespace DASHBOARD.Domain.Entities;

/// <summary>
/// Join linking a <see cref="SprintTask"/> (work item) to a selected <see cref="RepositoryMetadata"/>
/// catalog value — one generic mechanism serving Labels, Components, versions, and any future catalog key.
/// </summary>
public sealed class WorkItemMetadata : Common.BaseEntity
{
    /// <summary>Gets or sets the work item the value is attached to.</summary>
    public Guid SprintTaskId { get; set; }

    /// <summary>Gets or sets the assigned repository metadata catalog value.</summary>
    public Guid MetadataId { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    /// <summary>The owning work item.</summary>
    public SprintTask? SprintTask { get; set; }

    /// <summary>The assigned catalog value.</summary>
    public RepositoryMetadata? Metadata { get; set; }
}
