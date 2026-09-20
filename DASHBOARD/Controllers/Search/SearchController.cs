using DASHBOARD.Application.Search.DTOs;
using DASHBOARD.Application.Search.Queries.GlobalSearch;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace DASHBOARD.Controllers.Search;

/// <summary>Global keyword search across a repository's Backlog and Sprint tasks.</summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/search")]
[Authorize]
public sealed class SearchController(ISender mediator) : ControllerBase
{
    /// <summary>Searches the repository for the given keyword, returning a unified grouped result list.</summary>
    /// <param name="repoId">The repository to search within.</param>
    /// <param name="q">The keyword (at least 2 characters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with matching items (empty when the term is too short).</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SearchResultItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Search(Guid repoId, [FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(Array.Empty<SearchResultItemDto>());

        var result = await mediator.Send(new GlobalSearchQuery(repoId, q), ct);
        return Ok(result);
    }
}
