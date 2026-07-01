namespace DASHBOARD.Domain.Enums;

/// <summary>Granular permission flags assignable to a <see cref="DASHBOARD.Domain.Entities.Role"/>.</summary>
public enum SystemFunction
{
    ViewRepository,
    EditRepository,
    ManageSettings,
    CreateWorkItem,
    EditWorkItem,
    DeleteWorkItem,
    ManageSprint,
    ManageCapacity,
    ManageWiki,
    ManageBoard,
}
