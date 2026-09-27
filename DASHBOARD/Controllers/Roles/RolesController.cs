using DASHBOARD.Application.Roles.Commands.CloneRole;
using DASHBOARD.Application.Roles.Commands.CreateRole;
using DASHBOARD.Application.Roles.Commands.DeleteRole;
using DASHBOARD.Application.Roles.Commands.UpdateRole;
using DASHBOARD.Application.Roles.DTOs;
using DASHBOARD.Application.Roles.Queries.ListRoles;
using DASHBOARD.Controllers.Roles.Requests;
using DASHBOARD.Core.Attributes;
using DASHBOARD.Core.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace DASHBOARD.Controllers.Roles;

/// <summary>Manages roles available to a repository — global default roles and repository-scoped custom roles.</summary>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/repositories/{repoId:guid}/roles")]
[Authorize]
public sealed class RolesController(ISender mediator) : ControllerBase
{
    /// <summary>Returns all roles available in the repository (global defaults plus custom roles).</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the list of <see cref="RoleDto"/>.</returns>
    [HttpGet]
    // Deliberately revalidate-only rather than a long max-age: this same controller creates, updates
    // and deletes roles, so an admin who just edited one must not be served their own stale list from
    // the browser cache. The ETag keeps the payload off the wire while the list is unchanged.
    [ClientCache(CacheProfiles.RevalidateSeconds)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(Guid repoId, CancellationToken ct)
    {
        var result = await mediator.Send(new ListRolesQuery(repoId), ct);
        return Ok(result);
    }

    /// <summary>Creates a new custom role in the repository.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="request">Name, description, and allowed functions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the new <see cref="RoleDto"/>.</returns>
    [HttpPost]
    [ProducesResponseType<RoleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(Guid repoId, [FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateRoleCommand(repoId, request.Name, request.Description ?? string.Empty, request.Permissions), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Updates an existing custom role's name, description, and allowed functions. Default roles cannot be modified.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="roleId">The role to update.</param>
    /// <param name="request">New name, description, and functions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPut("{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid repoId, Guid roleId, [FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateRoleCommand(repoId, roleId, request.Name, request.Description ?? string.Empty, request.Permissions), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Clones an existing role (default or custom) into a new custom role within the same repository.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="roleId">The source role to clone.</param>
    /// <param name="request">Name for the cloned role.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the cloned <see cref="RoleDto"/>.</returns>
    [HttpPost("{roleId:guid}/clone")]
    [ProducesResponseType<RoleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Clone(Guid repoId, Guid roleId, [FromBody] CloneRoleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CloneRoleCommand(repoId, roleId, request.NewName), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Deletes a custom role. Fails for default roles or when members are still assigned.</summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="roleId">The role to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 400 when the role is default or members are still assigned.</returns>
    [HttpDelete("{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid repoId, Guid roleId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteRoleCommand(repoId, roleId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }
}
