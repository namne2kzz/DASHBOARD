using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Controllers.Internal;

/// <summary>
/// Machine-to-machine endpoints consumed by HUB services over the internal Docker network.
/// Protected by <see cref="Middleware.InternalApiKeyMiddleware"/> (X-Internal-Token header) — no JWT.
/// </summary>
/// <param name="db">Read-only access to the application database.</param>
[ApiController]
[Route("internal/v1")]
[AllowAnonymous]
public sealed class InternalController(IApplicationDbContext db) : ControllerBase
{
    // ── Users ────────────────────────────────────────────────────────────────

    /// <summary>Returns a user's public profile. Used by HUB dashboard-gateway to resolve display names.</summary>
    /// <param name="id">User id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with profile, or 404.</returns>
    [HttpGet("users/{id:guid}")]
    [ProducesResponseType<InternalUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
    {
        var user = await db.Set<User>()
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new InternalUserDto(u.Id, u.Name, u.Email, u.AvatarClass, u.IsGlobalAdmin))
            .FirstOrDefaultAsync(ct);

        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Returns multiple user profiles in one call. HUB passes a list of known user ids to resolve names.</summary>
    /// <param name="ids">Comma-separated user ids.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with matched profiles (unknown ids silently omitted).</returns>
    [HttpGet("users")]
    [ProducesResponseType<IReadOnlyList<InternalUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] string ids, CancellationToken ct)
    {
        var parsed = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue).Select(g => g!.Value)
            .ToList();

        if (parsed.Count == 0) return Ok(Array.Empty<InternalUserDto>());

        var result = await db.Set<User>()
            .AsNoTracking()
            .Where(u => parsed.Contains(u.Id))
            .Select(u => new InternalUserDto(u.Id, u.Name, u.Email, u.AvatarClass, u.IsGlobalAdmin))
            .ToListAsync(ct);

        return Ok(result);
    }

    // ── Memberships ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a user's repository memberships (roles + permissions).
    /// HUB dashboard-gateway uses this to verify workspace access and cache permissions.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with memberships, or 404 if user doesn't exist.</returns>
    [HttpGet("users/{id:guid}/memberships")]
    [ProducesResponseType<InternalMembershipsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMemberships(Guid id, CancellationToken ct)
    {
        var user = await db.Set<User>()
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new { u.IsGlobalAdmin, u.OrgId })
            .FirstOrDefaultAsync(ct);

        if (user is null) return NotFound();

        // Resolve the user's organization (tenant) — HUB uses this as the workspace.
        var org = await db.Set<Organization>().AsNoTracking()
            .Where(o => o.Id == user.OrgId)
            .Select(o => new { o.Alias, o.Name })
            .FirstOrDefaultAsync(ct);

        // Fetch from DB first (AllowedFunctions is a JSON column — .ToString() on enums
        // cannot be translated to SQL, so projection must happen in memory after hydration).
        var rows = await db.Set<RepositoryMember>()
            .AsNoTracking()
            .Include(m => m.Repository)
            .Include(m => m.Role)
            .Where(m => m.UserId == id)
            .ToListAsync(ct);

        var memberships = rows.Select(m => new InternalRepositoryMembershipDto(
            m.RepositoryId,
            m.Repository!.Name,
            m.Repository.Code,
            m.Repository.IsArchived,
            m.Role != null ? m.Role.Name : m.DefaultRole,
            m.DefaultRole,
            m.Role != null
                ? m.Role.AllowedFunctions.Select(f => f.ToString()).ToList()
                : [])).ToList();

        return Ok(new InternalMembershipsDto(
            id, user.IsGlobalAdmin, user.OrgId, org?.Alias ?? string.Empty, org?.Name ?? string.Empty, memberships));
    }

    // ── Repositories ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all user profiles that are members of a repository/workspace.
    /// HUB dashboard-gateway calls this to populate the workspace member sidebar.
    /// </summary>
    /// <param name="id">Repository id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with member profile list (empty array if repository not found or has no members).</returns>
    [HttpGet("repositories/{id:guid}/members")]
    [ProducesResponseType<IReadOnlyList<InternalUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRepositoryMembers(Guid id, CancellationToken ct)
    {
        var members = await (
            from rm in db.Set<RepositoryMember>().AsNoTracking()
            join u  in db.Set<User>().AsNoTracking() on rm.UserId equals u.Id
            where rm.RepositoryId == id
            select new InternalUserDto(u.Id, u.Name, u.Email, u.AvatarClass, u.IsGlobalAdmin)
        ).ToListAsync(ct);

        return Ok(members);
    }

    // ── User settings ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all persisted preference settings for a user.
    /// HUB dashboard-gateway calls this so the HUB shell can honour the user's date/timezone preferences
    /// without requiring them to configure them again inside HUB.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with a flat key→value map; 404 if the user does not exist.</returns>
    [HttpGet("users/{id:guid}/settings")]
    [ProducesResponseType(typeof(Dictionary<string, string?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserSettings(Guid id, CancellationToken ct)
    {
        // Verify user exists first — we don't want to silently return an empty dict for a bad id.
        var exists = await db.Set<User>().AsNoTracking().AnyAsync(u => u.Id == id, ct);
        if (!exists) return NotFound();

        var rows = await db.Set<UserSetting>()
            .AsNoTracking()
            .Where(s => s.UserId == id)
            .Select(s => new { s.Key, s.Value })
            .ToListAsync(ct);

        return Ok(rows.ToDictionary(r => r.Key, r => r.Value));
    }

    // ── Work items ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns context for a work item — used by HUB when linking a discussion thread.
    /// Returns key, title, state, and owning repository so the thread can display the context.
    /// </summary>
    /// <param name="id">SprintTask (work item) id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with context, or 404.</returns>
    [HttpGet("work-items/{id:guid}")]
    [ProducesResponseType<InternalWorkItemContextDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkItem(Guid id, CancellationToken ct)
    {
        // Join manually — SprintTask has no Repository navigation property.
        var item = await (
            from t in db.Set<SprintTask>().AsNoTracking()
            join r in db.Set<Repository>().AsNoTracking() on t.RepositoryId equals r.Id
            where t.Id == id && !t.IsDeleted
            select new InternalWorkItemContextDto(
                t.Id,
                SprintTask.BuildWorkItemNumber(r.Code, t.WorkItemNumber),
                t.Title,
                t.State.ToString(),
                t.RepositoryId,
                r.Code)
        ).FirstOrDefaultAsync(ct);

        return item is null ? NotFound() : Ok(item);
    }
}

// ── DTOs (internal — not exposed via public Swagger) ─────────────────────────

/// <summary>Minimal user profile for HUB consumption.</summary>
public sealed record InternalUserDto(Guid Id, string Name, string Email, string AvatarClass, bool IsGlobalAdmin);

/// <summary>A user's membership in one repository — field names mirror DashboardGateway Contracts.RepositoryMembership.</summary>
public sealed record InternalRepositoryMembershipDto(
    Guid RepositoryId,
    string RepositoryName,
    string RepositoryCode,
    bool IsArchived,
    string RoleName,
    string DefaultRole,
    IReadOnlyList<string> Permissions);

/// <summary>All repository memberships for a user, plus the owning organization (tenant = HUB workspace).</summary>
public sealed record InternalMembershipsDto(
    Guid UserId,
    bool IsGlobalAdmin,
    Guid OrgId,
    string OrgAlias,
    string OrgName,
    IReadOnlyList<InternalRepositoryMembershipDto> Repositories);

/// <summary>Work item context for linking a HUB discussion thread.</summary>
public sealed record InternalWorkItemContextDto(
    Guid Id, string Key, string Title, string State, Guid RepositoryId, string RepositoryCode);
