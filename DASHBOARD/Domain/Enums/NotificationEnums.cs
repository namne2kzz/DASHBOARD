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

    /// <summary>User was added as a member of a repository.</summary>
    AddedToRepository = 6,

    /// <summary>User was removed from a repository.</summary>
    RemovedFromRepository = 7,

    /// <summary>User was added to a sprint's capacity.</summary>
    AddedToSprint = 8,

    /// <summary>User was removed from a sprint's capacity.</summary>
    RemovedFromSprint = 9,
}
