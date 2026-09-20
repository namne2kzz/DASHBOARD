using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.History.DTOs;
using DASHBOARD.Application.History.Queries.ListRepositoryHistory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace DASHBOARD.Controllers.History;

/// <summary>Exposes the repository-wide, filterable audit log. Restricted to repository admins.</summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/audit-log")]
[Authorize]
public sealed class AuditLogController(ISender mediator) : ControllerBase
{
    /// <summary>Returns a paged, filtered slice of the repository's audit trail, newest first.</summary>
    /// <param name="repoId">The repository to audit.</param>
    /// <param name="authorId">Optional author filter.</param>
    /// <param name="from">Optional inclusive lower bound (UTC).</param>
    /// <param name="to">Optional inclusive upper bound (UTC).</param>
    /// <param name="category">Optional category — "created", "state", "assignment", or "update".</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page, max 100 (default 30).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with a paged <see cref="AuditLogEntryDto"/> list, 403 when not a repo admin.</returns>
    [HttpGet]
    [ProducesResponseType<PagedResult<AuditLogEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(
        Guid repoId,
        [FromQuery] Guid?     authorId = null,
        [FromQuery] DateTime? from     = null,
        [FromQuery] DateTime? to       = null,
        [FromQuery] string?   category = null,
        [FromQuery] int       page     = 1,
        [FromQuery] int       pageSize = 30,
        CancellationToken ct = default)
    {
        try
        {
            var result = await mediator.Send(
                new ListRepositoryHistoryQuery(repoId, authorId, from, to, category, page, pageSize), ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
