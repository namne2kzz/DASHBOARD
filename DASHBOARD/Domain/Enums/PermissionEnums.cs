namespace DASHBOARD.Domain.Enums;

/// <summary>Granular permission flags assignable to a <see cref="DASHBOARD.Domain.Entities.Role"/>.</summary>
public enum SystemFunction
{
    // ── Repository ─────────────────────────────────────────────── 0-1
    /// <summary>Read-only access to the repository and all its data.</summary>
    ViewRepository  = 0,
    /// <summary>Edit repository info (name, description, code).</summary>
    EditRepository  = 1,

    // ── Members & Access ──────────────────────────────────────── 2-5
    /// <summary>Add, remove, and change the role of repository members.</summary>
    ManageMembers   = 2,
    /// <summary>Create, edit, clone, and delete custom permission roles.</summary>
    ManageRoles     = 3,
    /// <summary>Send email invitations to join the repository.</summary>
    InviteMembers   = 4,
    /// <summary>Edit repository metadata catalog (RepoRole labels, custom keys).</summary>
    ManageMetadata  = 5,

    // ── Work Items (Sprint Tasks) ──────────────────────────────── 6-9
    /// <summary>Create new sprint tasks (User Story, Task, Bug, TestPlan).</summary>
    CreateWorkItem  = 6,
    /// <summary>Edit sprint tasks, change state, log work, update remaining.</summary>
    EditWorkItem    = 7,
    /// <summary>Delete sprint tasks from the repository.</summary>
    DeleteWorkItem  = 8,
    /// <summary>Assign or unassign sprint tasks to team members.</summary>
    AssignWorkItem  = 9,

    // ── Backlog ───────────────────────────────────────────────── 10-11
    /// <summary>Create, update, delete, and rank product backlog items.</summary>
    ManageBacklog   = 10,
    /// <summary>Promote backlog items into a sprint; descope sprint tasks back to backlog.</summary>
    PromoteToSprint = 11,

    // ── Sprint ────────────────────────────────────────────────── 12-13
    /// <summary>Create, update, and delete sprint definitions.</summary>
    ManageSprint    = 12,
    /// <summary>Activate a planned sprint and close a running sprint.</summary>
    ActivateSprint  = 13,

    // ── Capacity ─────────────────────────────────────────────── 14
    /// <summary>Manage sprint capacity entries and team day-offs.</summary>
    ManageCapacity  = 14,

    // ── Wiki ─────────────────────────────────────────────────── 15
    /// <summary>Create, edit, move, and delete wiki pages.</summary>
    ManageWiki      = 15,

    // ── Board ────────────────────────────────────────────────── 16
    /// <summary>Configure workflow board columns, WIP limits, and aging thresholds.</summary>
    ManageBoard     = 16,

    // ── Analytics & Integrations ──────────────────────────────── 17-19
    /// <summary>View analytics dashboards and velocity/burndown charts.</summary>
    ViewAnalytics   = 17,
    /// <summary>Configure and trigger CI/CD pipeline definitions.</summary>
    ManagePipeline  = 18,
    /// <summary>Manage repository integration settings (linked repos, webhooks).</summary>
    ManageRepo      = 19,
}
