using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Domain.Entities;

/// <summary>A time-boxed iteration within a repository. Follows Azure DevOps / Scrum terminology.</summary>
public sealed class Sprint : Common.BaseEntity
{
    /// <summary>Gets or sets the owning repository.</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the sprint display name (e.g. "Sprint 1 - May 2026").</summary>
    public string Name { get; set; } = default!;

    /// <summary>Gets or sets the sprint start date (inclusive).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Gets or sets the sprint end date (inclusive).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Gets or sets the explicit lifecycle status of this sprint.</summary>
    public SprintStatus Status { get; set; } = SprintStatus.Planning;

    /// <summary>Gets or sets the UTC timestamp when the sprint was closed; null until closed.</summary>
    public DateTime? ClosedAt { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Capacity rows for team members in this sprint.</summary>
    public ICollection<CapacityMember> CapacityMembers { get; set; } = [];

    /// <summary>Days-off entries for this sprint.</summary>
    public ICollection<DayOff> DaysOff { get; set; } = [];

    /// <summary>Tasks planned for this sprint.</summary>
    public ICollection<SprintTask> Tasks { get; set; } = [];
}
