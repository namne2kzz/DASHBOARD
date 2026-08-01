using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Domain.Entities;

/// <summary>One-time invite sent to an external email to onboard a new Google-authenticated user into a repository.</summary>
public sealed class Invitation : Common.BaseEntity
{
    /// <summary>Gets or sets the email address the invite was sent to.</summary>
    public string Email { get; set; } = default!;

    /// <summary>Gets or sets the repository the invitee will be added to upon acceptance.</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the user who issued this invite.</summary>
    public Guid InvitedByUserId { get; set; }

    /// <summary>Gets or sets the role (default or custom) that will be granted to the invitee on acceptance. Required.</summary>
    public Guid RoleId { get; set; }

    /// <summary>Gets or sets the team role (discipline) the invitee will be assigned on acceptance. Stores a RepoRole metadata value. Does not grant permissions.</summary>
    public string DefaultRole { get; set; } = string.Empty;

    /// <summary>Gets or sets the manager the invitee's new user account will report to, in the organisation hierarchy. Null means no manager. Applied only when a new user is created on acceptance.</summary>
    public Guid? ManagerId { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the raw token embedded in the invite link.
    /// The raw token is never persisted — only this hash is stored and used for lookup.
    /// </summary>
    public string TokenHash { get; set; } = default!;

    /// <summary>Gets or sets the UTC timestamp after which this invite can no longer be accepted.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the invitee accepted. Null until accepted.</summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>Gets or sets the current lifecycle status of this invite.</summary>
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Repository the invitee will join on acceptance.</summary>
    public Repository? Repository { get; set; }

    /// <summary>User who created this invite.</summary>
    public User? InvitedBy { get; set; }

    /// <summary>The role the invitee will be granted on acceptance.</summary>
    public Role? Role { get; set; }
}
