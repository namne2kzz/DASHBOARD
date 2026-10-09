namespace DASHBOARD.Controllers.Sprints.Requests;

/// <summary>Body for the close-sprint endpoint.</summary>
public sealed record CloseSprintRequest(
    /// <summary>When true, close the sprint even if incomplete items remain.</summary>
    bool Force = false);
