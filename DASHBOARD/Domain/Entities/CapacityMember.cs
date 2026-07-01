namespace DASHBOARD.Domain.Entities;

/// <summary>Capacity configuration for one team member within a sprint.</summary>
public sealed class CapacityMember : Common.BaseEntity
{
    /// <summary>Gets or sets the sprint this capacity row belongs to.</summary>
    public Guid SprintId { get; set; }

    /// <summary>Gets or sets the repository (denormalized for query isolation).</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the team member.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the member's team role (discipline) for this sprint. Stores a RepoRole metadata value.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Gets or sets the regular working hours per day (0–12).</summary>
    public decimal HoursPerDay { get; set; }

    /// <summary>Gets or sets the overtime hours per day (0–6).</summary>
    public decimal OvertimeHoursPerDay { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Parent sprint.</summary>
    public Sprint? Sprint { get; set; }

    /// <summary>Team member user.</summary>
    public User? User { get; set; }
}
