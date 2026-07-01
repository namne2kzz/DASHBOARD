namespace DASHBOARD.Controllers.Repositories.Requests;

/// <summary>HTTP request body for updating a repository's name and description.</summary>
/// <param name="Name">New project name.</param>
/// <param name="Description">New description.</param>
public sealed record UpdateRepositoryRequest(string Name, string Description = "");
