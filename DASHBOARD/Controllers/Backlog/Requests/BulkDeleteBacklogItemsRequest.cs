namespace DASHBOARD.Controllers.Backlog.Requests;

/// <summary>HTTP request body for deleting multiple backlog items at once.</summary>
/// <param name="ItemIds">The backlog items to delete.</param>
public sealed record BulkDeleteBacklogItemsRequest(IReadOnlyList<Guid> ItemIds);
