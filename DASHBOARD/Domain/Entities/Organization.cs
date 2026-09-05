namespace DASHBOARD.Domain.Entities;

/// <summary>
/// Top-level tenant — one <see cref="Organization"/> represents one company that Nexus sold a license to.
/// Owns <see cref="Repository"/> and <see cref="User"/> records. <see cref="Alias"/> is the URL-safe
/// tenant prefix used across both DASHBOARD and HUB routes.
/// </summary>
public sealed class Organization : Common.BaseEntity
{
    /// <summary>Gets or sets the organization display name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Gets or sets the URL-safe unique alias used as the tenant prefix in routes (e.g. "nexus"). Unique system-wide.</summary>
    public string Alias { get; set; } = default!;

    /// <summary>Gets or sets the primary contact email for the organization.</summary>
    public string ContactEmail { get; set; } = default!;

    /// <summary>Gets or sets an optional description of the organization.</summary>
    public string About { get; set; } = string.Empty;

    /// <summary>Gets or sets the activated license key (base64, opaque). Unique — one key binds to at most one organization.</summary>
    public string LicenseKey { get; set; } = default!;

    /// <summary>Gets or sets the license payment due date (snapshot copied at activation).</summary>
    public DateTime LicenseDueDate { get; set; }

    /// <summary>Gets or sets the license expiry date (snapshot copied at activation).</summary>
    public DateTime LicenseExpireDate { get; set; }

    /// <summary>Gets or sets the maximum number of repositories allowed under this license (snapshot copied at activation).</summary>
    public int LicenseRepoCapacity { get; set; }
}
