namespace DASHBOARD.Controllers.Repositories.Requests;

/// <summary>HTTP request body for updating a repository's editable details.</summary>
/// <remarks>
/// <c>Code</c> is deliberately not editable: it is the URL segment and the work-item key prefix,
/// so changing it would break every link already shared.
/// </remarks>
/// <param name="Name">New project name.</param>
/// <param name="Description">New description.</param>
/// <param name="LeadId">
/// The member to hold the Project Manager role. Omit to leave the current holder in place.
/// </param>
public sealed record UpdateRepositoryRequest(
    string Name,
    string Description = "",
    Guid?  LeadId = null);
