namespace DASHBOARD.Controllers.Sprints.Requests;

/// <summary>Request body for editing an existing discussion comment.</summary>
/// <param name="Body">The replacement Markdown comment body.</param>
public sealed record UpdateDiscussionRequest(string Body);
