using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Users.Commands.ChangePassword;
using DASHBOARD.Application.Users.Commands.CreateUser;
using DASHBOARD.Application.Users.Commands.SetUserManager;
using DASHBOARD.Application.Users.Commands.ToggleActive;
using DASHBOARD.Application.Users.Commands.ToggleAdmin;
using DASHBOARD.Application.Users.Commands.UpdateProfile;
using DASHBOARD.Application.Users.DTOs;
using DASHBOARD.Application.Users.Queries.GetUser;
using DASHBOARD.Application.Users.Queries.GetUserHierarchy;
using DASHBOARD.Application.Users.Queries.ListUsers;
using DASHBOARD.Application.Users.Queries.SearchUsers;
using DASHBOARD.Controllers.Users.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.Users;

/// <summary>Manages user profiles and password changes.</summary>
[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Searches active users by name or email fragment. Accessible to any authenticated user; used by the member-picker flow.</summary>
    /// <param name="q">Search term — at least 2 characters.</param>
    /// <param name="excludeRepoId">When provided, users already in this repository are excluded from results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with up to 20 matching <see cref="UserPickerItemDto"/> items.</returns>
    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyList<UserPickerItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] Guid?  excludeRepoId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(Array.Empty<UserPickerItemDto>());

        var result = await mediator.Send(new SearchUsersQuery(q.Trim(), excludeRepoId), ct);
        return Ok(result);
    }

    /// <summary>Returns a paged list of all users. Restricted to global admins.</summary>
    /// <param name="search">Optional name/email filter.</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page, max 100 (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with paged user list.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListUsersQuery(search, page, Math.Clamp(pageSize, 1, 100)), ct);
        return Ok(result);
    }

    /// <summary>Creates a new system user account. Restricted to global admins.</summary>
    /// <param name="request">Name, email, password, avatar color class, and optional global-admin flag.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 with the created <see cref="SystemUserListItemDto"/>, 400 on validation failure, 409 on duplicate email.</returns>
    [HttpPost]
    [ProducesResponseType<SystemUserListItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(
                new CreateUserCommand(request.Name, request.Email, request.Password, request.IsGlobalAdmin, request.AvatarClass, request.ManagerId), ct);
            return CreatedAtAction(nameof(Get), new { userId = result.UserId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Returns the public profile of a specific user.</summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="UserProfileDto"/>, or 404 if not found.</returns>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetUserQuery(userId), ct);
        return Ok(result);
    }

    /// <summary>Returns the profile of the currently authenticated user.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with <see cref="UserProfileDto"/>.</returns>
    [HttpGet("me")]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await mediator.Send(new GetUserQuery(currentUser.UserId!.Value), ct);
        return Ok(result);
    }

    /// <summary>Updates the display name and avatar class of a user. Only the user themselves or a global admin may call this.</summary>
    /// <param name="userId">The user to update.</param>
    /// <param name="request">The new name and avatar class.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 400 on validation failure, 403 if not authorised.</returns>
    [HttpPut("{userId:guid}/profile")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(Guid userId, [FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateProfileCommand(userId, request.Name, request.AvatarClass), ct);
        if (result.IsFailure) return Forbid();
        return NoContent();
    }

    /// <summary>Changes the password of the authenticated user after verifying the current password.</summary>
    /// <param name="userId">Must match the authenticated user's ID.</param>
    /// <param name="request">Old and new plaintext passwords.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 400 on wrong old password or validation failure, 403 if not the account owner.</returns>
    [HttpPut("{userId:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangePassword(Guid userId, [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new ChangePasswordCommand(userId, request.OldPassword, request.NewPassword), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Activates or deactivates a user account by toggling the soft-delete flag. Only a global admin may call this.</summary>
    /// <param name="userId">The user to activate or deactivate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 403 if not authorised or targeting self, 404 if user not found.</returns>
    [HttpPatch("{userId:guid}/activate")]
    [HttpPatch("{userId:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(Guid userId, CancellationToken ct)
    {
        var result = await mediator.Send(new ToggleActiveCommand(userId), ct);
        if (result.IsFailure) return Forbid();
        return NoContent();
    }

    /// <summary>Sets or clears a user's manager in the organisation hierarchy. Global admins only.</summary>
    /// <param name="userId">The user whose manager is being set.</param>
    /// <param name="request">The new manager's ID, or null to clear.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 400 on validation failure (self/cycle/missing), 403 if not a global admin.</returns>
    [HttpPatch("{userId:guid}/manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetManager(Guid userId, [FromBody] SetUserManagerRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SetUserManagerCommand(userId, request.ManagerId), ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    /// <summary>Returns the organisation-chart slice centred on a user (managers, peers, and reports). Global admins only.</summary>
    /// <param name="userId">The focus user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the <see cref="UserHierarchyDto"/>, 403 if not a global admin.</returns>
    [HttpGet("{userId:guid}/hierarchy")]
    [ProducesResponseType<UserHierarchyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHierarchy(Guid userId, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(new GetUserHierarchyQuery(userId), ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Promote or demote the target user for global admin role. Only a global admin may call this.</summary>
    /// <param name="userId">The user to update.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success, 400 on validation failure, 403 if not authorised.</returns>
    [HttpPatch("{userId:guid}/promote-admin")]
    [HttpPatch("{userId:guid}/demote-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleAdmin(Guid userId, CancellationToken ct)
    {
        var result = await mediator.Send(new ToggleAdminCommand(userId), ct);
        if (result.IsFailure) return Forbid();
        return NoContent();
    }
}
