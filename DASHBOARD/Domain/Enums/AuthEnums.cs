namespace DASHBOARD.Domain.Enums;

/// <summary>Authentication provider used to create the user's account.</summary>
public enum AuthProvider
{
    /// <summary>Account created by a system administrator — authenticates with username/password.</summary>
    System,

    /// <summary>Account created via Google invite flow — authenticates with Google OAuth.</summary>
    Google,
}

/// <summary>Lifecycle status of a repository invite.</summary>
public enum InvitationStatus
{
    /// <summary>Token issued, awaiting acceptance.</summary>
    Pending,

    /// <summary>Invite was accepted and the user account created.</summary>
    Accepted,

    /// <summary>Token TTL elapsed before acceptance.</summary>
    Expired,

    /// <summary>Manually revoked by an admin (e.g. a re-invite was issued).</summary>
    Revoked,
}
