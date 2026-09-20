using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Discussions.Commands.AddDiscussion;
using DASHBOARD.Application.Discussions.DTOs;
using DASHBOARD.Application.Discussions.Queries.ListDiscussions;
using DASHBOARD.Application.History.DTOs;
using DASHBOARD.Application.History.Queries.ListHistory;
using DASHBOARD.Application.SprintTasks.Commands.SetSprintTaskMetadata;
using DASHBOARD.Application.SprintTasks.Commands.UpdateSprintTask;
using DASHBOARD.Application.SprintTasks.DTOs;
using DASHBOARD.Application.SprintTasks.Queries.GetSprintTaskDetail;
using DASHBOARD.Application.SprintTasks.Queries.GetSprintTaskMetadata;
using DASHBOARD.Application.SprintTasks.Queries.ResolveSprintTaskKey;
using DASHBOARD.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace DASHBOARD.Controllers.Sprints;

/// <summary>Provides task detail, discussions, and history for a single sprint task — independent of which sprint it sits in.</summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/sprint-tasks/{taskId:guid}")]
[Authorize]
public sealed class SprintTaskDetailController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Resolves a formatted work-item key (e.g. "DASH-10") to its sprint task UUID.
    /// This route is intentionally absolute so it does not inherit the /{taskId:guid} segment.
    /// </summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="key">Formatted work-item key, e.g. "DASH-10".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the task UUID as a JSON string; 404 if not found.</returns>
    [HttpGet("/api/repositories/{repoId:guid}/sprint-tasks/by-key/{key}")]
    [ProducesResponseType<Guid>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveByKey(Guid repoId, string key, CancellationToken ct)
    {
        var taskId = await mediator.Send(new ResolveSprintTaskKeyQuery(repoId, key), ct);
        return Ok(taskId);
    }

    /// <summary>Returns the full detail for a single sprint task.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The sprint task ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the <see cref="SprintTaskDto"/>.</returns>
    [HttpGet]
    [ProducesResponseType<SprintTaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid repoId, Guid taskId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSprintTaskDetailQuery(repoId, taskId), ct);
        return Ok(result);
    }

    /// <summary>Updates the editable fields of a sprint task. State and assignee are changed via their own dedicated endpoints, not here.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The sprint task ID.</param>
    /// <param name="request">Updated field values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid repoId, Guid taskId, [FromBody] UpdateSprintTaskDetailRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateSprintTaskCommand(
            repoId, taskId,
            request.Title, request.Description ?? string.Empty, request.Priority,
            request.AssignedToId, request.StoryPoints, request.OriginalEstimate,
            request.StepsToReproduce, request.Environment, request.RootCause,
            request.Solution, request.Impaction, request.UnitTest, request.DesignReview,
            request.TestSteps, request.Automated,
            request.AcceptanceCriteria, request.Documents,
            request.RemainingWork,
            request.ParentId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Returns the discussion thread for a sprint task, oldest first.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The sprint task ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the list of <see cref="DiscussionDto"/>.</returns>
    [HttpGet("discussions")]
    [ProducesResponseType<IReadOnlyList<DiscussionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListDiscussions(Guid repoId, Guid taskId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListDiscussionsQuery(repoId, taskId), ct);
        return Ok(result);
    }

    /// <summary>Posts a new comment on the sprint task's discussion thread.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The sprint task ID.</param>
    /// <param name="body">The comment text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new <see cref="DiscussionDto"/>.</returns>
    [HttpPost("discussions")]
    [ProducesResponseType<DiscussionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddDiscussion(Guid repoId, Guid taskId, [FromBody] string body, CancellationToken ct)
    {
        var result = await mediator.Send(new AddDiscussionCommand(repoId, taskId, body), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Returns the metadata catalog values (labels, components, versions) assigned to a sprint task.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The sprint task ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the assigned <see cref="WorkItemMetadataDto"/> list.</returns>
    [HttpGet("metadata")]
    [ProducesResponseType<IReadOnlyList<WorkItemMetadataDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMetadata(Guid repoId, Guid taskId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSprintTaskMetadataQuery(repoId, taskId), ct);
        return Ok(result);
    }

    /// <summary>Replaces the full set of metadata catalog values assigned to a sprint task.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The sprint task ID.</param>
    /// <param name="request">The complete desired set of metadata value IDs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 400 on business-rule violation.</returns>
    [HttpPut("metadata")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetMetadata(Guid repoId, Guid taskId, [FromBody] SetWorkItemMetadataRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SetSprintTaskMetadataCommand(repoId, taskId, request.MetadataIds ?? []), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Returns the audit-trail history for a sprint task, newest first.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The sprint task ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the list of <see cref="HistoryDto"/>.</returns>
    [HttpGet("history")]
    [ProducesResponseType<IReadOnlyList<HistoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListHistory(Guid repoId, Guid taskId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListHistoryQuery(repoId, taskId), ct);
        return Ok(result);
    }
}

/// <summary>Request body for updating a sprint task's editable fields (excludes state and assignee — see their own endpoints).</summary>
public sealed record UpdateSprintTaskDetailRequest(
    string           Title,
    string?          Description,
    WorkItemPriority Priority,
    Guid?            AssignedToId,
    int              StoryPoints,
    decimal          OriginalEstimate,
    string?          StepsToReproduce,
    string?          Environment,
    string?          RootCause,
    string?          Solution,
    string?          Impaction,
    string?          UnitTest,
    string?          DesignReview,
    List<string>?    TestSteps,
    bool?            Automated,
    Guid?            ParentId,
    string?          AcceptanceCriteria,
    List<string>?    Documents,
    decimal          RemainingWork);

/// <summary>Request body for replacing a work item's metadata catalog assignments.</summary>
/// <param name="MetadataIds">The complete desired set of repository metadata value IDs.</param>
public sealed record SetWorkItemMetadataRequest(IReadOnlyList<Guid> MetadataIds);
