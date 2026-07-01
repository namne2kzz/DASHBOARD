namespace DASHBOARD.Domain.Entities;

/// <summary>A day-off entry deducting hours from a member's (or the whole team's) sprint capacity.</summary>
public sealed class DayOff : Common.BaseEntity
{
    /// <summary>Gets or sets the sprint this day off belongs to.</summary>
    public Guid SprintId { get; set; }

    /// <summary>Gets or sets the repository (denormalized for query isolation).</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the affected user. Null means the day off applies to the whole team.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Gets or sets the date of the day off.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the number of hours to deduct from capacity.</summary>
    public decimal Hours { get; set; }

    /// <summary>Gets or sets a short reason (e.g. "Public holiday", "Sick leave").</summary>
    public string Reason { get; set; } = string.Empty;

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Parent sprint.</summary>
    public Sprint? Sprint { get; set; }

    /// <summary>Affected user. Null when this is a team-wide day off.</summary>
    public User? User { get; set; }
}
