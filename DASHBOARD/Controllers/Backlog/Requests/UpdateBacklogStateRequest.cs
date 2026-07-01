using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Controllers.Backlog.Requests;

/// <summary>HTTP request body for transitioning a backlog item's refinement state.</summary>
/// <param name="State">Target state (New = 0, Refining = 1, Ready = 2).</param>
public sealed record UpdateBacklogStateRequest(BacklogItemState State);
