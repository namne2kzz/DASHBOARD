using DASHBOARD.Domain.Interfaces;

namespace DASHBOARD.Domain.Entities;

/// <summary>A wiki page within a repository. Supports a tree structure via <see cref="ParentId"/>.</summary>
public sealed class WikiPage : Common.BaseEntity, ISoftDelete
{
    /// <summary>Gets or sets the owning repository.</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the page title.</summary>
    public string Title { get; set; } = default!;

    /// <summary>Gets or sets the page content (HTML or Markdown).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp of the last content update.</summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>Gets or sets the parent page ID for tree hierarchy. Null means this is a root page.</summary>
    public Guid? ParentId { get; set; }

    // ── ISoftDelete ──────────────────────────────────────────────────────────
    /// <inheritdoc/>
    public bool IsDeleted { get; set; }

    /// <inheritdoc/>
    public DateTime? DeletedAt { get; set; }

    /// <inheritdoc/>
    public Guid? DeletedByUserId { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Parent page. Null for root pages.</summary>
    public WikiPage? Parent { get; set; }

    /// <summary>Child pages.</summary>
    public ICollection<WikiPage> Children { get; set; } = [];
}
