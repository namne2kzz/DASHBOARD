using DASHBOARD.Application.Notifications.DTOs;
using DASHBOARD.Domain.Enums;
using MediatR;

namespace DASHBOARD.Application.Notifications.Queries.ListNotifications;

/// <summary>Returns a page of the authenticated user's notifications (newest first) with paging and an optional type filter.</summary>
/// <param name="UnreadOnly">When true, returns only unread notifications.</param>
/// <param name="Category">Optional category filter: "workitems" | "sprints" | "membership" | "mentions". Null = all.</param>
/// <param name="Skip">Number of notifications to skip (for load-more paging).</param>
/// <param name="Take">Page size (clamped server-side).</param>
public sealed record ListNotificationsQuery(
    bool    UnreadOnly = false,
    string? Category   = null,
    int     Skip       = 0,
    int     Take       = 30) : IRequest<NotificationPageDto>;
