namespace DASHBOARD.Infrastructure.Settings;

/// <summary>
/// Per-project GitHub connection list, keyed by <c>Repository.Code</c>. Bound from the
/// <c>"GitConnections"</c> configuration section — lives in server-side configuration
/// (gitignored dev file locally, environment variables/Key Vault in Azure) and is never
/// stored in the database or accepted from the frontend.
/// </summary>
public sealed class GitConnectionsOptions : Dictionary<string, List<GitConnectionEntry>>
{
    /// <summary>The configuration section name this options type binds from.</summary>
    public const string SectionName = "GitConnections";
}

/// <summary>One configured link between a project and an external GitHub repository.</summary>
public sealed class GitConnectionEntry
{
    /// <summary>Gets or sets the full HTTPS clone URL of the GitHub repository (e.g. <c>https://github.com/owner/repo.git</c>).</summary>
    public string RepoUrl { get; set; } = default!;

    /// <summary>Gets or sets the GitHub Personal Access Token used to authenticate calls to this repository. Never returned in any DTO or logged.</summary>
    public string Token { get; set; } = default!;

    /// <summary>Gets or sets an optional override for the repository's default branch, avoiding a GitHub API round-trip to detect it.</summary>
    public string? DefaultBranchOverride { get; set; }

    /// <summary>Gets or sets whether this is the primary connection shown by default when a project has multiple. If no entry is marked primary, the first entry in the list is treated as primary.</summary>
    public bool IsPrimary { get; set; }
}
