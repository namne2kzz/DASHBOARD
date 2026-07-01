using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Wiki.Commands.CreateWikiPage;
using DASHBOARD.Application.Wiki.Commands.DeleteWikiPage;
using DASHBOARD.Application.Wiki.Commands.MoveWikiPage;
using DASHBOARD.Application.Wiki.Commands.UpdateWikiPage;
using DASHBOARD.Application.Wiki.DTOs;
using DASHBOARD.Application.Wiki.Queries.GetWikiPage;
using DASHBOARD.Application.Wiki.Queries.ListWikiPages;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.Wiki;

/// <summary>Manages the repository wiki: hierarchical pages with Markdown/HTML content.</summary>
[ApiController]
[Route("api/repositories/{repoId:guid}/wiki")]
[Authorize]
public sealed class WikiController(ISender mediator) : ControllerBase
{
    /// <summary>Returns the full wiki page tree for the repository.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with root pages and their children recursively.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(Guid repoId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListWikiPagesQuery(repoId), ct);
        return Ok(result);
    }

    /// <summary>Returns a single wiki page with its direct children.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="pageId">The page ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="WikiPageDto"/>.</returns>
    [HttpGet("{pageId:guid}")]
    [ProducesResponseType<WikiPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid repoId, Guid pageId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetWikiPageQuery(repoId, pageId), ct);
        return Ok(result);
    }

    /// <summary>Creates a new wiki page.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="title">Page title.</param>
    /// <param name="content">Page content (HTML or Markdown).</param>
    /// <param name="parentId">Parent page ID; omit for a root page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new <see cref="WikiPageDto"/>.</returns>
    [HttpPost]
    [ProducesResponseType<WikiPageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        Guid repoId,
        [FromQuery] string  title,
        [FromQuery] string  content  = "",
        [FromQuery] Guid?   parentId = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new CreateWikiPageCommand(repoId, title, content, parentId), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Updates the title and content of a wiki page.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="pageId">The page to update.</param>
    /// <param name="title">New title.</param>
    /// <param name="content">New content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPut("{pageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid repoId, Guid pageId, [FromQuery] string title, [FromQuery] string content, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateWikiPageCommand(repoId, pageId, title, content), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Moves a wiki page to a new parent. Prevents circular references.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="pageId">The page to move.</param>
    /// <param name="newParentId">The new parent page ID, or omit to make it a root page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 400 on circular reference.</returns>
    [HttpPatch("{pageId:guid}/move")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Move(Guid repoId, Guid pageId, [FromQuery] Guid? newParentId, CancellationToken ct)
    {
        var result = await mediator.Send(new MoveWikiPageCommand(repoId, pageId, newParentId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Soft-deletes a wiki page. Fails if it has children.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="pageId">The page to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 400 when children block deletion.</returns>
    [HttpDelete("{pageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid repoId, Guid pageId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteWikiPageCommand(repoId, pageId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
