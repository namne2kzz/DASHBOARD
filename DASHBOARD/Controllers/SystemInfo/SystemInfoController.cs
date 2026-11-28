using Asp.Versioning;
using DASHBOARD.Application.SystemInfo.DTOs;
using DASHBOARD.Application.SystemInfo.Queries.GetAbout;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.SystemInfo;

/// <summary>Information about the running API itself, for the About dialog.</summary>
/// <param name="mediator">Dispatches the about query.</param>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/system")]
[Authorize]
public sealed class SystemInfoController(ISender mediator) : ControllerBase
{
    /// <summary>Returns this build's version and the live status of every service the API depends on.</summary>
    /// <remarks>
    /// Requires a signed-in user: the response names internal hosts, which is not something to
    /// hand out anonymously. Every probe reads only — none of them writes to a dependency.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the <see cref="AboutDto"/>.</returns>
    [HttpGet("about")]
    [ProducesResponseType<AboutDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AboutDto>> GetAbout(CancellationToken ct)
        => Ok(await mediator.Send(new GetAboutQuery(), ct));
}
