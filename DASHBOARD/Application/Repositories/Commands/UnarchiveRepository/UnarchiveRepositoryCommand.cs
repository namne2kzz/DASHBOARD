using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Repositories.Commands.UnarchiveRepository;

/// <summary>Restores an archived repository to ordinary views. Restricted to global admins.</summary>
/// <remarks>
/// The counterpart to <c>ArchiveRepositoryCommand</c>. Archiving hides a repository without
/// deleting anything, so it must be reversible — otherwise it is a delete wearing a softer name.
/// </remarks>
/// <param name="RepositoryId">The repository to restore.</param>
public sealed record UnarchiveRepositoryCommand(Guid RepositoryId) : IRequest<Result>;
