using MediatR;
using DASHBOARD.Application.Common.Models;

namespace DASHBOARD.Application.Repositories.Commands.ArchiveRepository;

/// <summary>Soft-deletes a repository, hiding it from all member views. Restricted to global admins. This operation is reversible via direct DB update.</summary>
/// <param name="RepositoryId">The repository to archive.</param>
public sealed record ArchiveRepositoryCommand(Guid RepositoryId) : IRequest<Result>;
