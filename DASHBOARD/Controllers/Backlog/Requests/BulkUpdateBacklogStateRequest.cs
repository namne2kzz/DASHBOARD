using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Controllers.Backlog.Requests;

/// <summary>HTTP request body for transitioning multiple backlog items to the same refinement state.</summary>
/// <param name="ItemIds">The backlog items to update.</param>
/// <param name="State">Target state (New = 0, Refining = 1, Ready = 2).</param>
public sealed record BulkUpdateBacklogStateRequest(IReadOnlyList<Guid> ItemIds, BacklogItemState State);
