namespace DASHBOARD.Domain.Enums;

/// <summary>
/// The relationship a <see cref="Entities.WorkItemLink"/> expresses from its source work item to its target.
/// Parent/child hierarchy is NOT modelled here — it stays on <see cref="Entities.SprintTask.ParentId"/>.
/// Links are stored once in the source→target direction; the opposite item shows the inverse label.
/// </summary>
public enum WorkItemLinkType
{
    /// <summary>Non-directional relation between two items.</summary>
    Related = 0,

    /// <summary>Source blocks target (target is blocked by source).</summary>
    Blocks = 1,

    /// <summary>Source is a duplicate of target.</summary>
    Duplicate = 2,

    /// <summary>Source depends on target (target is a dependency of source).</summary>
    DependsOn = 3,

    /// <summary>Source is a successor of target (target is the predecessor).</summary>
    Successor = 4,

    /// <summary>Source affects target (target is affected by source).</summary>
    Affects = 5,

    /// <summary>Source tests target (target is tested by source) — e.g. a test plan verifying a story.</summary>
    Tests = 6,

    /// <summary>Source is caused by target (target causes source) — e.g. a bug caused by a change.</summary>
    CausedBy = 7,
}
