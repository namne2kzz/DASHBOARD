using DASHBOARD.Application.MyWork.DTOs;
using DASHBOARD.Application.MyWork.Queries.GetMyWork;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.MyWork;

/// <summary>Exposes the current user's personal cross-repository work queue.</summary>
[ApiController]
[Route("api/my-work")]
[Authorize]
public sealed class MyWorkController(ISender mediator) : ControllerBase
{
    /// <summary>Returns every open work item assigned to the authenticated user across all repositories they belong to.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the user's assigned open work items, highest priority first.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MyWorkItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyWorkQuery(), ct);
        return Ok(result);
    }
}
