using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Repositories.Commands.ArchiveRepository;
using DASHBOARD.Application.Repositories.Commands.CreateRepository;
using DASHBOARD.Application.Repositories.Commands.UpdateRepository;
using DASHBOARD.Application.Repositories.DTOs;
using DASHBOARD.Application.Repositories.Queries.CheckRepositoryCode;
using DASHBOARD.Application.Repositories.Queries.GetRepository;
using DASHBOARD.Application.Repositories.Queries.ListRepositories;
using DASHBOARD.Controllers.Repositories.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.Repositories;

/// <summary>Manages project repositories (create, read, update, archive).</summary>
[ApiController]
[Route("api/repositories")]
[Authorize]
public sealed class RepositoriesController(ISender mediator) : ControllerBase
{
    /// <summary>Returns all repositories visible to the authenticated user.</summary>
    /// <param name="search">Optional name/code filter.</param>
    /// <param name="includeArchived">When true, archived repositories are included (global admins only).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the list of <see cref="RepositoryDto"/>.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] bool    includeArchived = false,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListRepositoriesQuery(search, includeArchived), ct);
        return Ok(result);
    }

    /// <summary>Returns a single repository by ID.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="RepositoryDto"/>, or 404 if not found or not accessible.</returns>
    [HttpGet("{repoId:guid}")]
    [ProducesResponseType<RepositoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid repoId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetRepositoryQuery(repoId), ct);
        return Ok(result);
    }

    /// <summary>Checks whether a repository code is available. Restricted to global admins.</summary>
    /// <param name="code">The candidate code to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <c>{ "available": true/false }</c>.</returns>
    [HttpGet("check-code")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckCode([FromQuery] string code, CancellationToken ct)
    {
        var available = await mediator.Send(new CheckRepositoryCodeQuery(code), ct);
        return Ok(new { available });
    }

    /// <summary>Creates a new repository. Restricted to global admins.</summary>
    /// <param name="request">Name, code, and description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new <see cref="RepositoryDto"/>, or 403 if not a global admin.</returns>
    [HttpPost]
    [ProducesResponseType<RepositoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateRepositoryRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateRepositoryCommand(request.Name, request.Code, request.Description, request.ScrumMasterId), ct);
        return CreatedAtAction(nameof(Get), new { repoId = result.Id }, result);
    }

    /// <summary>Updates the name and description of an existing repository.</summary>
    /// <param name="repoId">The repository to update.</param>
    /// <param name="request">New name and description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 403 on insufficient permission.</returns>
    [HttpPut("{repoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid repoId, [FromBody] UpdateRepositoryRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateRepositoryCommand(repoId, request.Name, request.Description), ct);
        if (result.IsFailure) return Forbid();
        return NoContent();
    }

    /// <summary>Archives a repository, hiding it from all views. Restricted to global admins.</summary>
    /// <param name="repoId">The repository to archive.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 403 if not a global admin.</returns>
    [HttpDelete("{repoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid repoId, CancellationToken ct)
    {
        var result = await mediator.Send(new ArchiveRepositoryCommand(repoId), ct);
        if (result.IsFailure) return Forbid();
        return NoContent();
    }
}
