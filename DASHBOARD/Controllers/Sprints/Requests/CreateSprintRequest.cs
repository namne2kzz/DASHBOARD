namespace DASHBOARD.Controllers.Sprints.Requests;

/// <summary>HTTP request body for creating a sprint.</summary>
/// <param name="Name">Sprint display name.</param>
/// <param name="StartDate">Inclusive start date.</param>
/// <param name="EndDate">Inclusive end date.</param>
public sealed record CreateSprintRequest(string Name, DateOnly StartDate, DateOnly EndDate, bool CreateHubChannel);
