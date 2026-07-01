namespace DASHBOARD.Controllers.Repositories.Requests;

/// <summary>HTTP request body for creating a new repository.</summary>
/// <param name="Name">Full project name.</param>
/// <param name="Code">Short uppercase prefix used in work-item IDs.</param>
/// <param name="Description">Optional project description.</param>
/// <param name="ScrumMasterId">ID of the existing user to assign as Scrum Master.</param>
public sealed record CreateRepositoryRequest(string Name, string Code, Guid ScrumMasterId, string Description = "");
