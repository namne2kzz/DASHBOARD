using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.History.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.History.Queries.ListRepositoryHistory;

/// <summary>
/// Handles <see cref="ListRepositoryHistoryQuery"/>: returns a paged, filterable audit log for the whole
/// repository. Restricted to members holding <see cref="SystemFunction.ManageMembers"/> (repo admins).
/// </summary>
public sealed class ListRepositoryHistoryQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<ListRepositoryHistoryQuery, PagedResult<AuditLogEntryDto>>
{
    /// <summary>Validates admin permission, applies the filters in SQL, and returns the requested page.</summary>
    /// <param name="query">The audit-log query with filters and paging.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of audit-log rows, newest first.</returns>
    public async Task<PagedResult<AuditLogEntryDto>> Handle(ListRepositoryHistoryQuery query, CancellationToken ct)
    {
        if (!await user.CanAsync(query.RepositoryId, SystemFunction.ManageMembers, ct))
            throw new UnauthorizedAccessException("You do not have permission to view the audit log for this repository.");

        var repoCode = await db.Set<Repository>().AsNoTracking()
            .Where(r => r.Id == query.RepositoryId)
            .Select(r => r.Code)
            .FirstAsync(ct);

        var q = db.Set<HistoryEntry>().AsNoTracking()
            .Where(h => h.RepositoryId == query.RepositoryId);

        if (query.AuthorId.HasValue)
            q = q.Where(h => h.AuthorId == query.AuthorId.Value);
        if (query.FromUtc.HasValue)
            q = q.Where(h => h.CreatedAt >= query.FromUtc.Value);
        if (query.ToUtc.HasValue)
            q = q.Where(h => h.CreatedAt <= query.ToUtc.Value);

        // Category filter — all branches translate to SQL so paging stays correct.
        q = query.Category?.ToLower() switch
        {
            "state"      => q.Where(h => h.Message.ToLower().Contains("state changed")),
            "assignment" => q.Where(h => h.Message.ToLower().Contains("assign")),
            "created"    => q.Where(h => h.Message.ToLower().Contains("created")),
            "update"     => q.Where(h => !h.Message.ToLower().Contains("state changed")
                                      && !h.Message.ToLower().Contains("assign")
                                      && !h.Message.ToLower().Contains("created")),
            _            => q,
        };

        var total = await q.CountAsync(ct);

        var page     = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var rows = await q
            .OrderByDescending(h => h.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new
            {
                h.Id, h.SprintTaskId,
                WorkItemNumber = h.SprintTask!.WorkItemNumber,
                h.AuthorId, AuthorName = h.Author!.Name, h.Author.AvatarClass,
                h.Message, h.CreatedAt,
            })
            .ToListAsync(ct);

        var items = rows
            .Select(r => new AuditLogEntryDto(
                r.Id, r.SprintTaskId, SprintTask.BuildWorkItemNumber(repoCode, r.WorkItemNumber),
                r.AuthorId, r.AuthorName, r.AvatarClass, r.Message, Classify(r.Message), r.CreatedAt))
            .ToList();

        return new PagedResult<AuditLogEntryDto>(items, total, page, pageSize);
    }

    /// <summary>Derives a coarse display category from a history message. @param message The change message.</summary>
    private static string Classify(string message)
    {
        var m = message.ToLowerInvariant();
        if (m.Contains("state changed")) return "state";
        if (m.Contains("assign"))        return "assignment";
        if (m.Contains("created"))       return "created";
        return "update";
    }
}
