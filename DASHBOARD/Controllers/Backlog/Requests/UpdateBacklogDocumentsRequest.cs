namespace DASHBOARD.Controllers.Backlog.Requests;

/// <summary>HTTP request body for replacing the document list of a backlog item.</summary>
/// <param name="Documents">Ordered list of document titles or links.</param>
public sealed record UpdateBacklogDocumentsRequest(IReadOnlyList<string> Documents);
