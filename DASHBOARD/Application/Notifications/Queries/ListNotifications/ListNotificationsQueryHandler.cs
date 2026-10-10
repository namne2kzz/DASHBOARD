using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Notifications.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Notifications.Queries.ListNotifications;

/// <summary>Handles <see cref="ListNotificationsQuery"/>: fetches the caller's most recent notifications from the database.</summary>
public sealed class ListNotificationsQueryHandler(
    IApplicationDbContext db,
    ICurrentUserService   currentUser) : IRequestHandler<ListNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private const int PageSize = 30;

    /// <summary>Returns up to 30 notifications for the authenticated user, newest first.</summary>
    /// <param name="query">The list query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of <see cref="NotificationDto"/>.</returns>
    public async Task<IReadOnlyList<NotificationDto>> Handle(ListNotificationsQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();

        var q = db.Set<Notification>()
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (query.UnreadOnly)
            q = q.Where(n => !n.IsRead);

        return await q
            .OrderByDescending(n => n.CreatedAt)
            .Take(PageSize)
            .Select(n => new NotificationDto(
                n.Id, n.Type.ToString(), n.Title, n.Body, n.Url, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
    }
}
