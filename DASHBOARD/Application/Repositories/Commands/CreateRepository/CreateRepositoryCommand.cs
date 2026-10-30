using DASHBOARD.Application.Repositories.DTOs;
using MediatR;

namespace DASHBOARD.Application.Repositories.Commands.CreateRepository;

/// <summary>Creates a new project repository and assigns a specified user as Project Manager. Restricted to global admins.</summary>
/// <param name="Name">Full project name.</param>
/// <param name="Code">Short uppercase prefix (max 10 chars) used in work-item IDs (e.g. "DASH").</param>
/// <param name="Description">Optional project description.</param>
/// <param name="ProjectManagerId">
/// ID of the existing user to lead the new repository. They are seeded as its first member with
/// the Project Manager role, which carries every permission — somebody has to be able to invite
/// the rest of the team.
/// </param>
public sealed record CreateRepositoryCommand(
    string Name,
    string Code,
    string Description,
    Guid   ProjectManagerId) : IRequest<RepositoryDto>;
