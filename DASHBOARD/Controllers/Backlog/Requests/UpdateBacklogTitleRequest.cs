namespace DASHBOARD.Controllers.Backlog.Requests;

/// <summary>HTTP request body for renaming a backlog item.</summary>
/// <param name="Title">New title (1–500 chars).</param>
public sealed record UpdateBacklogTitleRequest(string Title);
