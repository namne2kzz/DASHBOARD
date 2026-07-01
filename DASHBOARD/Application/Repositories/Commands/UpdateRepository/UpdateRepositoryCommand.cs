using MediatR;
using DASHBOARD.Application.Common.Models;

namespace DASHBOARD.Application.Repositories.Commands.UpdateRepository;

/// <summary>Updates the name and description of an existing repository. Requires <see cref="DASHBOARD.Domain.Enums.SystemFunction.EditRepository"/>.</summary>
/// <param name="RepositoryId">The repository to update.</param>
/// <param name="Name">New project name.</param>
/// <param name="Description">New description.</param>
public sealed record UpdateRepositoryCommand(
    Guid   RepositoryId,
    string Name,
    string Description) : IRequest<Result>;
