using Asp.Versioning;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Application.WorkItemLinks.Commands.CreateWorkItemLink;
using DASHBOARD.Application.WorkItemLinks.Commands.DeleteWorkItemLink;
using DASHBOARD.Application.WorkItemLinks.DTOs;
using DASHBOARD.Application.WorkItemLinks.Queries.ListWorkItemLinks;
using DASHBOARD.Application.WorkItemLinks.Queries.SearchLinkTargets;
using DASHBOARD.Controllers.WorkItemLinks.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.WorkItemLinks;

/// <summary>Manages typed relationships between work items.</summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/sprint-tasks/{taskId:guid}/links")]
[Authorize]
public sealed class WorkItemLinksController(ISender mediator) : ControllerBase
{
    /// <summary>Lists the links involving the work item, from its perspective.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The work item ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the list of <see cref="WorkItemLinkDto"/>.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WorkItemLinkDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(Guid repoId, Guid taskId, CancellationToken ct)
        => Ok(await mediator.Send(new ListWorkItemLinksQuery(repoId, taskId), ct));

    /// <summary>Searches work items to link to (any type, excluding the current item).</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The current work item (excluded from results).</param>
    /// <param name="q">Optional title/key search term.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with up to 20 matching <see cref="WorkItemPickerDto"/>.</returns>
    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyList<WorkItemPickerDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Search(Guid repoId, Guid taskId, [FromQuery] string? q, CancellationToken ct)
        => Ok(await mediator.Send(new SearchLinkTargetsQuery(repoId, taskId, q), ct));

    /// <summary>Creates a link from this work item to another.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The source work item ID.</param>
    /// <param name="request">Target item, link type and optional note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the created <see cref="WorkItemLinkDto"/>; 400 when rejected.</returns>
    [HttpPost]
    [ProducesResponseType<WorkItemLinkDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        Guid repoId, Guid taskId, [FromBody] CreateWorkItemLinkRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateWorkItemLinkCommand(
            repoId, taskId, request.TargetTaskId, request.LinkType, request.Description), ct);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Removes a link.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The work item ID (route only).</param>
    /// <param name="linkId">The link to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpDelete("{linkId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid repoId, Guid taskId, Guid linkId, CancellationToken ct)
    {
        await mediator.Send(new DeleteWorkItemLinkCommand(repoId, linkId), ct);
        return NoContent();
    }
}
