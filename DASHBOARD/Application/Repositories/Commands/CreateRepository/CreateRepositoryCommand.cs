using DASHBOARD.Application.Repositories.DTOs;
using MediatR;

namespace DASHBOARD.Application.Repositories.Commands.CreateRepository;

/// <summary>Creates a new project repository and assigns a specified user as Scrum Master. Restricted to global admins.</summary>
/// <param name="Name">Full project name.</param>
/// <param name="Code">Short uppercase prefix (max 10 chars) used in work-item IDs (e.g. "DASH").</param>
/// <param name="Description">Optional project description.</param>
/// <param name="ScrumMasterId">ID of the existing user to assign as Scrum Master of the new repository.</param>
public sealed record CreateRepositoryCommand(
    string Name,
    string Code,
    string Description,
    Guid   ScrumMasterId) : IRequest<RepositoryDto>;
