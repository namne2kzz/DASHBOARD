using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;

namespace DASHBOARD.Domain.Entities;

/// <summary>Application user. <see cref="IsGlobalAdmin"/> bypasses all repository-level permission checks.</summary>
public sealed class User : Common.BaseEntity, ISoftDelete
{
    /// <summary>Gets or sets the user's display name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Gets or sets the unique email address used as login identifier.</summary>
    public string Email { get; set; } = default!;

    /// <summary>Gets or sets the PBKDF2 password hash. Never expose in API responses.</summary>
    public string PasswordHash { get; set; } = default!;

    /// <summary>Gets or sets the cryptographically random salt used during hashing. Never expose in API responses.</summary>
    public string PasswordSalt { get; set; } = default!;

    /// <summary>Gets or sets the Tailwind CSS background class for the avatar chip (e.g. "bg-sky-600").</summary>
    public string AvatarClass { get; set; } = "bg-slate-600";

    /// <summary>Gets or sets whether this user bypasses all repository-level permission checks.</summary>
    public bool IsGlobalAdmin { get; set; }

    /// <summary>Gets or sets the authentication provider that created this account.</summary>
    public AuthProvider AuthProvider { get; set; } = AuthProvider.System;

    /// <summary>
    /// Gets or sets the Google OAuth subject identifier (<c>sub</c> claim).
    /// Populated only when <see cref="AuthProvider"/> is <see cref="AuthProvider.Google"/>.
    /// Used to look up the user on subsequent Google logins. Never changes even if the Google email does.
    /// </summary>
    public string? GoogleSubjectId { get; set; }

    /// <summary>
    /// Gets or sets the parent manager's identifier.
    /// Null means this user is a root (no manager above them).
    /// </summary>
    public Guid? ManagerId { get; set; }

    // ── ISoftDelete ──────────────────────────────────────────────────────────
    /// <inheritdoc/>
    public bool IsDeleted { get; set; }

    /// <inheritdoc/>
    public DateTime? DeletedAt { get; set; }

    /// <inheritdoc/>
    public Guid? DeletedByUserId { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Manager of this user. Null when <see cref="ManagerId"/> is null (root user).</summary>
    public User? Manager { get; set; }

    /// <summary>Users who report directly to this user.</summary>
    public ICollection<User> Subordinates { get; set; } = [];
}
