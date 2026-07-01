using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Controllers.Backlog.Requests;

/// <summary>HTTP request body for updating a backlog item.</summary>
public sealed record UpdateBacklogItemRequest(
    string                Title,
    BacklogItemState      State,
    Guid?                 SprintId           = null,
    int?                  StoryPoints        = null,
    TshirtSize?           TshirtSize         = null,
    string                AcceptanceCriteria = "",
    IReadOnlyList<string> Documents          = default!);
