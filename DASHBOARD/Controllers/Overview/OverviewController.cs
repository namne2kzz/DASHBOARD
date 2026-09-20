using DASHBOARD.Application.Overview.DTOs;
using DASHBOARD.Application.Overview.Queries.GetOverviewStats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace DASHBOARD.Controllers.Overview;

/// <summary>Provides aggregated overview and analytics statistics for a repository.</summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/overview")]
[Authorize]
public sealed class OverviewController(ISender mediator) : ControllerBase
{
    /// <summary>Returns aggregated overview statistics for the repository, optionally scoped to a sprint.</summary>
    /// <param name="repoId">The repository to query.</param>
    /// <param name="sprintId">Sprint ID to scope statistics; omit to use the active or most recent sprint.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="OverviewStatsDto"/>.</returns>
    [HttpGet]
    [ProducesResponseType<OverviewStatsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid repoId, [FromQuery] Guid? sprintId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOverviewStatsQuery(repoId, sprintId), ct);
        return Ok(result);
    }
}
