namespace DASHBOARD.Controllers.Backlog.Requests;

/// <summary>HTTP request body for assigning or clearing a backlog item's sprint.</summary>
/// <param name="SprintId">The sprint ID to assign, or <c>null</c> to unschedule the item.</param>
public sealed record MoveToIterationRequest(Guid? SprintId);
