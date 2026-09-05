using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Domain.Constants;

/// <summary>
/// Templates for the standard roles (and matching disciplines) created for every repository.
/// Holds names and permission sets only — no identifiers — so repositories generate their own
/// <see cref="Entities.Role"/> rows (with fresh ids) instead of referencing seeded GUIDs.
/// </summary>
public static class DefaultRoleDefinitions
{
    // ── Well-known names (also used as RepoRole discipline values) ────────────
    public const string ScrumMaster     = "Scrum Master";
    public const string ProjectManager  = "Project Manager";
    public const string Developer       = "Developer";
    public const string Tester          = "Tester";
    public const string BusinessAnalyst = "Business Analyst";

    /// <summary>A default-role template: name, description, and allowed permissions.</summary>
    /// <param name="Name">Role/discipline name.</param>
    /// <param name="Description">Human-readable purpose.</param>
    /// <param name="AllowedFunctions">Granted permissions.</param>
    public sealed record Definition(string Name, string Description, IReadOnlyList<SystemFunction> AllowedFunctions);

    /// <summary>All standard roles created for each repository, in display order.</summary>
    public static readonly IReadOnlyList<Definition> All =
    [
        new(ScrumMaster, "Full access to all repository functions.",
            Enum.GetValues<SystemFunction>()),

        new(ProjectManager, "Manages sprints, capacity, and team settings in addition to all work-item and backlog operations.",
            Enum.GetValues<SystemFunction>()),

        new(Developer, "Creates and edits sprint tasks, manages the backlog, and can promote items to sprints.",
            [
                SystemFunction.ViewRepository,
                SystemFunction.CreateWorkItem,
                SystemFunction.EditWorkItem,
                SystemFunction.AssignWorkItem,                SystemFunction.ViewAnalytics,
                SystemFunction.ManagePipeline,
                SystemFunction.ManageRepo,
            ]),

        new(Tester, "Creates and edits sprint tasks and backlog items; focused on quality and test coverage.",
            [
                SystemFunction.ViewRepository,
                SystemFunction.CreateWorkItem,
                SystemFunction.EditWorkItem,
                SystemFunction.AssignWorkItem,
                SystemFunction.ManageBacklog,                SystemFunction.ViewAnalytics,
            ]),

        new(BusinessAnalyst, "Manages the product backlog and requirements; read-only on sprint execution.",
            [
                SystemFunction.ViewRepository,
                SystemFunction.CreateWorkItem,
                SystemFunction.EditWorkItem,
                SystemFunction.DeleteWorkItem,
                SystemFunction.AssignWorkItem,
                SystemFunction.ManageBacklog,
                SystemFunction.PromoteToSprint,
                SystemFunction.ManageSprint,
                SystemFunction.ActivateSprint,                SystemFunction.ViewAnalytics,
            ]),
    ];
}
