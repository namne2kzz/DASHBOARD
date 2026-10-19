using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.WorkItemLinks;

/// <summary>Direction-aware display labels for <see cref="WorkItemLinkType"/>.</summary>
public static class WorkItemLinkLabels
{
    /// <summary>
    /// Returns the label to show for a link when viewed from a given side.
    /// </summary>
    /// <param name="type">The link type.</param>
    /// <param name="outward">True when viewing from the source (outward) side; false from the target (inward) side.</param>
    /// <returns>The human-readable relationship label.</returns>
    public static string For(WorkItemLinkType type, bool outward) => type switch
    {
        WorkItemLinkType.Related   => "Related to",
        WorkItemLinkType.Blocks    => outward ? "Blocks"        : "Blocked by",
        WorkItemLinkType.Duplicate => outward ? "Duplicate of"  : "Duplicated by",
        WorkItemLinkType.DependsOn => outward ? "Depends on"    : "Dependency of",
        WorkItemLinkType.Successor => outward ? "Successor of"  : "Predecessor of",
        WorkItemLinkType.Affects   => outward ? "Affects"       : "Affected by",
        WorkItemLinkType.Tests     => outward ? "Tests"         : "Tested by",
        WorkItemLinkType.CausedBy  => outward ? "Caused by"     : "Causes",
        _ => type.ToString(),
    };

    /// <summary>Whether a link type is symmetric (same label both directions, no inverse).</summary>
    /// <param name="type">The link type.</param>
    public static bool IsSymmetric(WorkItemLinkType type) => type == WorkItemLinkType.Related;
}
