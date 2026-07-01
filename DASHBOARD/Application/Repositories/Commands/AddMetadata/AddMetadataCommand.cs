using DASHBOARD.Application.Repositories.DTOs;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.Repositories.Commands.AddMetadata;

/// <summary>Adds a selectable metadata value to the catalog for the given key. Global entries require GlobalAdmin; repository entries require <see cref="SystemFunction.ManageSettings"/>.</summary>
/// <param name="RepositoryId">The repository context (used for permission check and as the owner for non-global entries).</param>
/// <param name="Key">The metadata key (e.g. FixedInVersion).</param>
/// <param name="Value">The value to add (e.g. "1.4.1").</param>
/// <param name="IsGlobal">When true, the entry is shared across all repositories (RepositoryId is cleared).</param>
public sealed record AddMetadataCommand(
    Guid        RepositoryId,
    MetadataKey Key,
    string      Value,
    bool        IsGlobal) : IRequest<RepositoryMetadataDto>;
