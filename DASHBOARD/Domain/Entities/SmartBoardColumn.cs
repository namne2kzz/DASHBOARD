using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Domain.Entities;

/// <summary>A column on the WIP-limited kanban board. Each column maps to exactly one sprint-task state.</summary>
public sealed class SmartBoardColumn : Common.BaseEntity
{
    /// <summary>Gets or sets the owning repository.</summary>
    public Guid RepositoryId { get; set; }

    /// <summary>Gets or sets the column display name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Gets or sets the sprint-task state whose items appear in this column.</summary>
    public SprintTaskState MappedState { get; set; }

    /// <summary>Gets or sets the maximum number of items allowed in this column simultaneously (0 = unlimited).</summary>
    public int WipLimit { get; set; }

    /// <summary>Gets or sets whether exceeding WIP only warns (Soft) or blocks the drag (Hard).</summary>
    public WipMode WipMode { get; set; }

    /// <summary>Gets or sets the number of days an item can stay in this column before it is flagged as aging.</summary>
    public int AgingLimitDays { get; set; }

    /// <summary>Gets or sets the left-to-right display order of this column (0-based).</summary>
    public int Order { get; set; }
}
