using DASHBOARD.Application.Common.Models;
using MediatR;

namespace DASHBOARD.Application.Repositories.Commands.UpdateMetadata;

/// <summary>Updates the value of an existing metadata catalog entry. Global entries require GlobalAdmin; repository entries require ManageSettings.</summary>
/// <param name="RepositoryId">The repository context (permission check for repo-scoped entries).</param>
/// <param name="MetadataId">The catalog entry to update.</param>
/// <param name="Value">The new value.</param>
public sealed record UpdateMetadataCommand(
    Guid   RepositoryId,
    Guid   MetadataId,
    string Value) : IRequest<Result>;
