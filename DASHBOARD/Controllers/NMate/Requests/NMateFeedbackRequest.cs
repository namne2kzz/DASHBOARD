namespace DASHBOARD.Controllers.NMate.Requests;

/// <summary>Body of <c>PUT /api/v1/nmate/messages/{id}/feedback</c>.</summary>
/// <param name="Rating">1 helpful, -1 not helpful.</param>
/// <param name="Comment">Optional comment.</param>
public sealed record NMateFeedbackRequest(short Rating, string? Comment);
