namespace DASHBOARD.Domain.Enums;

/// <summary>Classifies the event that triggered a notification.</summary>
public enum NotificationType
{
    /// <summary>User was assigned to a work item.</summary>
    WorkItemAssigned   = 0,

    /// <summary>User was unassigned from a work item.</summary>
    WorkItemUnassigned = 1,

    /// <summary>A work item the user is assigned to changed state.</summary>
    WorkItemStateChanged = 2,

    /// <summary>A sprint the user belongs to was activated.</summary>
    SprintActivated = 3,

    /// <summary>A sprint the user belongs to was closed.</summary>
    SprintClosed = 4,

    /// <summary>User was @mentioned in a discussion.</summary>
    MentionedInDiscussion = 5,
}
