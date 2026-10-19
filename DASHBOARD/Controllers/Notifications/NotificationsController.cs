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
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Disable response buffering so each SSE frame reaches the browser immediately.
        if (Response.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>() is { } bodyFeature)
            bodyFeature.DisableBuffering();

        // Commit the status line and headers to the wire now, before the first write. Without this
        // the MVC pipeline holds the response open until the body completes, so EventSource never
        // sees the 200 and onopen never fires.
        await Response.StartAsync(ct);

        // Send an initial keep-alive comment so the client knows the connection is live.
        await WriteRawAsync(": connected\n\n", ct);

        await foreach (var notification in hub.Subscribe(userId).WithCancellation(ct))
        {
            if (ct.IsCancellationRequested) break;

            var payload = JsonSerializer.Serialize(NotificationSseEvent.From(notification), Json);
            await WriteRawAsync($"event: notification\ndata: {payload}\n\n", ct);
        }
    }

    /// <summary>Returns a page of the authenticated user's notifications with an optional category filter.</summary>
    /// <param name="unreadOnly">When true, returns only unread notifications.</param>
    /// <param name="category">Optional filter: "workitems" | "sprints" | "membership" | "mentions".</param>
    /// <param name="skip">Number of notifications to skip (load-more paging).</param>
    /// <param name="take">Page size (max 50).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with a <see cref="NotificationPageDto"/>.</returns>
    [HttpGet]
    [ProducesResponseType<NotificationPageDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] string? category = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 30,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListNotificationsQuery(unreadOnly, category, skip, take), ct));

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
