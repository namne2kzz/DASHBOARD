using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Repositories.Commands.DeleteMetadata;

/// <summary>Soft-deletes a metadata catalog entry from a repository. Requires <see cref="DASHBOARD.Domain.Enums.SystemFunction.ManageMetadata"/>.</summary>
/// <param name="RepositoryId">The owning repository.</param>
/// <param name="MetadataId">The metadata entry ID to delete.</param>
public sealed record DeleteMetadataCommand(Guid RepositoryId, Guid MetadataId) : IRequest<Result>;
