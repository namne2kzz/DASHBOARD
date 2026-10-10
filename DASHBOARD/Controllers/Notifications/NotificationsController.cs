using System.Text;
using System.Text.Json;
using Asp.Versioning;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Notifications.Commands.MarkRead;
using DASHBOARD.Application.Notifications.DTOs;
using DASHBOARD.Application.Notifications.Queries.ListNotifications;
using DASHBOARD.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DASHBOARD.Controllers.Notifications;

/// <summary>
/// Delivers in-app notifications to the authenticated user.
/// <c>GET /stream</c> is a long-lived Server-Sent Events connection; the other endpoints
/// manage the notification list and read state.
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public sealed class NotificationsController(
    INotificationHub    hub,
    ICurrentUserService currentUser,
    ISender             mediator) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Opens a Server-Sent Events stream. The connection stays open until the client
    /// disconnects or the server restarts. Each event is a JSON-serialised
    /// <see cref="NotificationSseEvent"/> with <c>event: notification</c>.
    /// </summary>
    /// <param name="ct">Populated by ASP.NET Core when the client disconnects.</param>
    [HttpGet("stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task Stream(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Response.Headers.ContentType    = "text/event-stream";
        Response.Headers.CacheControl   = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // disable nginx buffering

        // Send an initial keep-alive comment so the client knows the connection is live.
        await WriteRawAsync(": connected\n\n", ct);

        await foreach (var notification in hub.Subscribe(userId).WithCancellation(ct))
        {
            if (ct.IsCancellationRequested) break;

            var payload = JsonSerializer.Serialize(NotificationSseEvent.From(notification), Json);
            await WriteRawAsync($"event: notification\ndata: {payload}\n\n", ct);
        }
    }

    /// <summary>Returns the 30 most recent notifications for the authenticated user.</summary>
    /// <param name="unreadOnly">When true, returns only unread notifications.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with notification list.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool unreadOnly = false, CancellationToken ct = default)
        => Ok(await mediator.Send(new ListNotificationsQuery(unreadOnly), ct));

    /// <summary>Marks a single notification as read.</summary>
    /// <param name="id">Notification id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkOne(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new MarkReadCommand(id), ct);
        return result.IsSuccess ? NoContent() : NotFound(result.Error);
    }

    /// <summary>Marks all unread notifications as read.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAll(CancellationToken ct)
    {
        await mediator.Send(new MarkReadCommand(), ct);
        return NoContent();
    }

    /// <summary>Writes a raw SSE-formatted string directly to the response body and flushes immediately.</summary>
    private async Task WriteRawAsync(string text, CancellationToken ct)
    {
        await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), ct);
        await Response.Body.FlushAsync(ct);
    }
}

/// <summary>Shape of the JSON payload inside each <c>event: notification</c> SSE frame.</summary>
/// <param name="Id">Notification id.</param>
/// <param name="Type">Notification type name.</param>
/// <param name="Title">Short title.</param>
/// <param name="Body">Supporting detail.</param>
/// <param name="Url">Optional deep-link.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
public sealed record NotificationSseEvent(
    Guid     Id,
    string   Type,
    string   Title,
    string   Body,
    string?  Url,
    DateTime CreatedAt)
{
    /// <summary>Maps a domain <see cref="Notification"/> to the SSE wire shape.</summary>
    public static NotificationSseEvent From(Notification n) =>
        new(n.Id, n.Type.ToString(), n.Title, n.Body, n.Url, n.CreatedAt);
}
