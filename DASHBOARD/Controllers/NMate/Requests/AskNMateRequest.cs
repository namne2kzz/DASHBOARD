namespace DASHBOARD.Controllers.NMate.Requests;

/// <summary>Body of <c>POST /api/v1/nmate/chat</c>.</summary>
/// <param name="ConversationId">Conversation to continue, or null to start one.</param>
/// <param name="Message">The question.</param>
/// <param name="Context">Where the user is in the app.</param>
public sealed record AskNMateRequest(Guid? ConversationId, string Message, AskNMateContext? Context);

/// <summary>Page context of a question.</summary>
/// <param name="Route">Current SPA route, e.g. <c>/acme/DASH/sprint-planning</c>.</param>
/// <param name="RepositoryId">Current repository, if any.</param>
public sealed record AskNMateContext(string? Route, Guid? RepositoryId);
