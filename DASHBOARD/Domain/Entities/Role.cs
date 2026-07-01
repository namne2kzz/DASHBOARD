using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Domain.Entities;

/// <summary>
/// A role that bundles a named set of <see cref="SystemFunction"/> permissions (stored as JSON).
/// Default roles (<see cref="IsDefault"/> = true) are global, seeded from the team-role set and not editable.
/// Custom roles are repository-scoped and managed by repository admins.
/// </summary>
public sealed class Role : Common.BaseEntity
{
    /// <summary>Gets or sets the repository this role belongs to. Null for global default roles.</summary>
    public Guid? RepositoryId { get; set; }

    /// <summary>Gets or sets a value indicating whether this is a seeded, non-editable default role.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Gets or sets the role display name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Gets or sets an optional description of the role's purpose.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the list of allowed system functions. Serialized as JSON in the database.</summary>
    public List<SystemFunction> AllowedFunctions { get; set; } = [];

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Repository this role belongs to. Null for global default roles.</summary>
    public Repository? Repository { get; set; }
}
