namespace DASHBOARD.Controllers.Backlog.Requests;

/// <summary>HTTP request body for reordering a backlog item.</summary>
/// <param name="PreviousItemId">Item immediately before the target, or <c>null</c> for top.</param>
/// <param name="NextItemId">Item immediately after the target, or <c>null</c> for bottom.</param>
public sealed record RankBacklogItemRequest(Guid? PreviousItemId, Guid? NextItemId);
