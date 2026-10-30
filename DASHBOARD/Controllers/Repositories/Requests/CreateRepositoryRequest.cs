namespace DASHBOARD.Controllers.Repositories.Requests;

/// <summary>HTTP request body for creating a new repository.</summary>
/// <param name="Name">Full project name.</param>
/// <param name="Code">Short uppercase prefix used in work-item IDs.</param>
/// <param name="ProjectManagerId">
/// ID of the existing user to lead the repository. They become its first member, holding the
/// Project Manager role so they can invite everybody else.
/// </param>
/// <param name="Description">Optional project description.</param>
public sealed record CreateRepositoryRequest(string Name, string Code, Guid ProjectManagerId, string Description = "");
