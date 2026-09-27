using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Repositories.Commands.AddMetadata;
using DASHBOARD.Application.Repositories.Commands.DeleteMetadata;
using DASHBOARD.Application.Repositories.Commands.UpdateMetadata;
using DASHBOARD.Application.Repositories.DTOs;
using DASHBOARD.Application.Repositories.Queries.ListMetadata;
using DASHBOARD.Controllers.Repositories.Requests;
using DASHBOARD.Core.Attributes;
using DASHBOARD.Core.Constants;
using DASHBOARD.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace DASHBOARD.Controllers.Repositories;

/// <summary>
/// Manages the repository metadata catalog â€” the set of selectable values for each
/// well-known metadata key (e.g. all released versions for FixedInVersion).
/// Members read the catalog; ManageSettings privilege is required to add or delete entries.
/// </summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/metadata")]
[Authorize]
public sealed class RepositoryMetadataController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Returns active catalog entries for the repository.
    /// Pass <paramref name="key"/> to load dropdown options for a specific field
    /// (e.g. <c>?key=FixedInVersion</c> â†’ all available versions).
    /// Omit <paramref name="key"/> to get the full catalog grouped by key.
    /// </summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="key">Optional metadata key filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the list of <see cref="RepositoryMetadataDto"/> (includes DisplayName per entry).</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(Guid repoId, [FromQuery] MetadataKey? key, CancellationToken ct)
    {
        var result = await mediator.Send(new ListMetadataQuery(repoId, key), ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns all well-known metadata key names with their display labels.
    /// Use this to build the key picker UI before the user selects a key.
    /// </summary>
    /// <returns>200 with the array of <c>{ key, displayName }</c> objects.</returns>
    [HttpGet("keys")]
    // Safe to hold for an hour: this is Enum.GetValues over a compile-time enum, so no runtime
    // mutation can change it — only a redeploy can, and that comes with a fresh page load.
    [ClientCache(CacheProfiles.ReferenceSeconds)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetKeys() =>
        Ok(Enum.GetValues<MetadataKey>()
            .Select(k => new { key = k.ToString(), displayName = k.GetDisplayName() }));

    /// <summary>Adds a new selectable value to the catalog for the given key. Global entries require GlobalAdmin; repository entries require ManageSettings.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="request">Key, value, and global flag.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new <see cref="RepositoryMetadataDto"/>.</returns>
    [HttpPost]
    [ProducesResponseType<RepositoryMetadataDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Add(Guid repoId, [FromBody] AddMetadataRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<MetadataKey>(request.Key, ignoreCase: true, out var key))
            return BadRequest(new { error = $"Unknown metadata key '{request.Key}'." });

        var result = await mediator.Send(new AddMetadataCommand(repoId, key, request.Value, request.IsGlobal), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Updates the value of an existing catalog entry. Global entries require GlobalAdmin; repository entries require ManageSettings.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="metadataId">The catalog entry to update.</param>
    /// <param name="request">The new value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPut("{metadataId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid repoId, Guid metadataId, [FromBody] UpdateMetadataRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateMetadataCommand(repoId, metadataId, request.Value), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Soft-deletes a catalog entry. The entry is hidden from future list queries.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="metadataId">The catalog entry ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpDelete("{metadataId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid repoId, Guid metadataId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteMetadataCommand(repoId, metadataId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
