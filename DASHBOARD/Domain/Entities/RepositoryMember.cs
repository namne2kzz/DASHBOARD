namespace DASHBOARD.Domain.Entities;

/// <summary>Many-to-many join between <see cref="User"/> and <see cref="Repository"/> that also carries the member's role and optional extra permissions.</summary>
public sealed class RepositoryMember : Common.BaseEntity
{
    /// <summary>Gets or sets the member user's identifier.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the repository the user belongs to.</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the assigned role (default or custom) that grants this member's <see cref="SystemFunction"/> permissions. Required.</summary>
    public Guid RoleId { get; set; }

    /// <summary>Gets or sets the member's team role (discipline) used for sprint capacity planning. Stores a RepoRole metadata value. Does not grant permissions.</summary>
    public string DefaultRole { get; set; } = string.Empty;

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>The member user.</summary>
    public User? User { get; set; }

    /// <summary>The repository this membership belongs to.</summary>
    public Repository? Repository { get; set; }

    /// <summary>The assigned role granting this member's permissions.</summary>
    public Role? Role { get; set; }
}
