namespace DASHBOARD.Controllers.Backlog.Requests;

/// <summary>HTTP request body for updating the acceptance criteria of a backlog item.</summary>
/// <param name="AcceptanceCriteria">New acceptance criteria text (max 4 000 chars).</param>
public sealed record UpdateBacklogAcceptanceCriteriaRequest(string AcceptanceCriteria);
