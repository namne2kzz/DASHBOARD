using DASHBOARD.Application.GitRepositories.DTOs;
using DASHBOARD.Application.GitRepositories.Queries.GetGitRepositoryOverview;
using DASHBOARD.Application.GitRepositories.Queries.ListGitRepositoryConnections;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace DASHBOARD.Controllers.GitRepositories;

/// <summary>
/// Read-only surface for a project's configured GitHub connections and their overview
/// (branches/commits/PRs/rate-limit). Connections themselves are configured server-side
/// (see <c>GitConnectionsOptions</c>) — there is no endpoint to create/edit/delete them.
/// </summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/git-repositories")]
[Authorize]
public sealed class GitRepositoriesController(ISender mediator) : ControllerBase
{
    /// <summary>Returns the GitHub connections configured for the project. Never includes access tokens.</summary>
    /// <param name="repoId">The project (repository) ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the list of <see cref="GitRepositoryConnectionSummaryDto"/> (empty if none configured).</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(Guid repoId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListGitRepositoryConnectionsQuery(repoId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns the GitHub overview for the project's connected repository, or a
    /// <c>HasConnection = false</c> placeholder when none is configured.
    /// </summary>
    /// <param name="repoId">The project (repository) ID.</param>
    /// <param name="repoUrl">Optional connection to select when the project has multiple; defaults to the primary connection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="GitRepositoryOverviewDto"/> — always 200, even when not connected.</returns>
    [HttpGet("overview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOverview(Guid repoId, [FromQuery] string? repoUrl, CancellationToken ct)
    {
        var result = await mediator.Send(new GetGitRepositoryOverviewQuery(repoId, repoUrl), ct);
        return Ok(result);
    }
}
