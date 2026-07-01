using DASHBOARD.Application.Repositories.DTOs;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.Repositories.Queries.ListMetadata;

/// <summary>
/// Returns all active metadata catalog entries for a repository.
/// When <paramref name="Key"/> is supplied only entries for that key are returned —
/// use this to populate a dropdown when a user is selecting a value for a specific field.
/// </summary>
/// <param name="RepositoryId">The repository to query.</param>
/// <param name="Key">Optional key filter (e.g. FixedInVersion).</param>
public sealed record ListMetadataQuery(
    Guid         RepositoryId,
    MetadataKey? Key = null) : IRequest<IReadOnlyList<RepositoryMetadataDto>>;
