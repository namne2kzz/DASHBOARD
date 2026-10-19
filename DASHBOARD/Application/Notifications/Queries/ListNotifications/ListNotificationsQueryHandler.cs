using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Notifications.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Notifications.Queries.ListNotifications;

/// <summary>Handles <see cref="ListNotificationsQuery"/>: fetches a page of the caller's notifications with an optional category filter.</summary>
public sealed class ListNotificationsQueryHandler(
    IApplicationDbContext db,
    ICurrentUserService   currentUser) : IRequestHandler<ListNotificationsQuery, NotificationPageDto>
{
    private const int MaxPageSize = 50;

    /// <summary>The notification types that belong to each UI category filter.</summary>
    private static readonly Dictionary<string, NotificationType[]> CategoryTypes = new()
    {
        ["workitems"]  = [NotificationType.WorkItemAssigned, NotificationType.WorkItemUnassigned, NotificationType.WorkItemStateChanged],
        ["sprints"]    = [NotificationType.SprintActivated, NotificationType.SprintClosed],
        ["membership"] = [NotificationType.AddedToRepository, NotificationType.RemovedFromRepository, NotificationType.AddedToSprint, NotificationType.RemovedFromSprint],
        ["mentions"]   = [NotificationType.MentionedInDiscussion],
    };

    /// <summary>Returns a page of notifications, the unread count, and whether more exist.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="NotificationPageDto"/>.</returns>
    public async Task<NotificationPageDto> Handle(ListNotificationsQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();

        var take = Math.Clamp(query.Take, 1, MaxPageSize);
        var skip = Math.Max(query.Skip, 0);

        var baseQuery = db.Set<Notification>()
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (query.UnreadOnly)
            baseQuery = baseQuery.Where(n => !n.IsRead);

        if (query.Category is not null && CategoryTypes.TryGetValue(query.Category, out var types))
            baseQuery = baseQuery.Where(n => types.Contains(n.Type));

        // Unread count is always for the whole inbox, independent of the current filter/page.
        var unreadCount = await db.Set<Notification>()
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        // Fetch one extra row to tell whether more pages exist, without a second COUNT.
        var rows = await baseQuery
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take + 1)
            .Select(n => new NotificationDto(
                n.Id, n.Type.ToString(), n.Title, n.Body, n.Url, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

        var hasMore = rows.Count > take;
        if (hasMore) rows.RemoveAt(rows.Count - 1);

        return new NotificationPageDto(rows, hasMore, unreadCount);
    }
}
