using Asp.Versioning;
using DASHBOARD.Application.Attachments.Commands.AddAttachment;
using DASHBOARD.Application.Attachments.Commands.DeleteAttachment;
using DASHBOARD.Application.Attachments.Commands.RequestAttachmentUploadUrl;
using DASHBOARD.Application.Attachments.DTOs;
using DASHBOARD.Application.Attachments.Queries.ListAttachments;
using DASHBOARD.Controllers.Attachments.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.Attachments;

/// <summary>Manages file attachments and pasted inline images for a work item.</summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/sprint-tasks/{taskId:guid}/attachments")]
[Authorize]
public sealed class AttachmentsController(ISender mediator) : ControllerBase
{
    /// <summary>Requests a presigned upload URL for an attachment or a pasted inline image.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The work item ID.</param>
    /// <param name="request">File metadata used to validate and build the object key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="AttachmentUploadUrlResult"/>; 400 when the file is rejected.</returns>
    [HttpPost("upload-url")]
    [ProducesResponseType<AttachmentUploadUrlResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RequestUploadUrl(
        Guid repoId, Guid taskId, [FromBody] RequestAttachmentUploadUrlRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RequestAttachmentUploadUrlCommand(
            repoId, taskId, request.FileName, request.ContentType, request.SizeBytes, request.InlineImage), ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Lists the work item's attachments, newest first.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The work item ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the attachment list.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AttachmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(Guid repoId, Guid taskId, CancellationToken ct)
        => Ok(await mediator.Send(new ListAttachmentsQuery(repoId, taskId), ct));

    /// <summary>Confirms an uploaded file and records it as an attachment.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The work item ID.</param>
    /// <param name="request">The uploaded file's metadata and object key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the created <see cref="AttachmentDto"/>; 400 when rejected.</returns>
    [HttpPost]
    [ProducesResponseType<AttachmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Add(
        Guid repoId, Guid taskId, [FromBody] AddAttachmentRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AddAttachmentCommand(
            repoId, taskId, request.FileName, request.ContentType, request.SizeBytes, request.ObjectKey), ct);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Deletes an attachment (uploader or board manager only).</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="taskId">The work item ID (route only — the attachment id identifies the row).</param>
    /// <param name="attachmentId">The attachment to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpDelete("{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid repoId, Guid taskId, Guid attachmentId, CancellationToken ct)
    {
        await mediator.Send(new DeleteAttachmentCommand(repoId, attachmentId), ct);
        return NoContent();
    }
}
