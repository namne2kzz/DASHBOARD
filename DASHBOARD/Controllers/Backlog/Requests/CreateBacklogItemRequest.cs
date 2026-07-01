using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Controllers.Backlog.Requests;

/// <summary>HTTP request body for creating a backlog item.</summary>
public sealed record CreateBacklogItemRequest(
    BacklogItemType Type,
    string          Title,
    Guid?           ParentId           = null,
    int?            StoryPoints        = null,
    TshirtSize?     TshirtSize         = null,
    string          AcceptanceCriteria = "");
