namespace DASHBOARD.Controllers.Sprints.Requests;

/// <summary>Request body for posting a comment or a reply on a sprint task's discussion thread.</summary>
/// <param name="Body">The Markdown comment body.</param>
/// <param name="ParentId">The comment being replied to, or null to post a root comment.</param>
public sealed record AddDiscussionRequest(string Body, Guid? ParentId = null);
