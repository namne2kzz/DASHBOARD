namespace DASHBOARD.Domain.Entities;

/// <summary>Project container — equivalent to a Jira Project or Azure DevOps Team Project. <see cref="Code"/> is the prefix for work-item IDs (e.g. "DASH").</summary>
public sealed class Repository : Common.BaseEntity
{
    /// <summary>Gets or sets the human-readable project name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Gets or sets the short unique prefix (max 10 chars, uppercase) used in work-item identifiers (e.g. "DASH" → DASH-1).</summary>
    public string Code { get; set; } = default!;

    /// <summary>Gets or sets an optional description for the repository/project.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the repository has been archived. Archived repositories are hidden from member views but not hard-deleted.</summary>
    public bool IsArchived { get; set; }
}
