using System.Text.Json;
using Asp.Versioning;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.NMate.DTOs;
using DASHBOARD.Controllers.NMate.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DASHBOARD.Controllers.NMate;

/// <summary>
/// The SPA's door to NMate, the AI support assistant. A thin authenticated relay: it adds the caller's
/// identity and forwards to NMate's internal API, which is never exposed publicly.
/// </summary>
/// <remarks>
/// No MediatR here on purpose: there is no DASHBOARD business logic to run — the use cases live in the NMate
/// service — and a streamed answer has to be copied through as it arrives, which a request/response handler
/// would only get in the way of. Only the endpoints listed below are forwarded; NMate's maintenance endpoints
/// (re-index, purge user data) are unreachable from the browser.
/// </remarks>
/// <param name="gateway">NMate gateway.</param>
/// <param name="currentUser">Authenticated user.</param>
[ApiVersion("1.0")][ApiController]
[Route("api/v{version:apiVersion}/nmate")]
[Authorize]
public sealed class NMateController(INMateGateway gateway, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Rate-limit policy name for <see cref="Chat"/> (registered in Program.cs).</summary>
    public const string ChatRateLimitPolicy = "nmate-chat";

    // Relaxed escaping: the body goes to NMate's JSON parser, never into HTML, so Vietnamese can stay readable.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Asks NMate a question. The response is <c>text/event-stream</c>: meta → delta… → citations → done (or error).</summary>
    /// <param name="request">Question and page context.</param>
    /// <param name="ct">Request-aborted token; closing the widget stops answer generation upstream.</param>
    /// <returns>A task that completes when the stream has been relayed.</returns>
    [HttpPost("chat")]
    [EnableRateLimiting(ChatRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task Chat([FromBody] AskNMateRequest request, CancellationToken ct) =>
        RelayAsync("POST", "chat", request, ct);

    /// <summary>Lists the user's NMate conversations, newest first.</summary>
    /// <param name="page">1-based page.</param>
    /// <param name="pageSize">Page size (1–50).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the response has been relayed.</returns>
    [HttpGet("conversations")]
    public Task Conversations([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        RelayAsync("GET", $"conversations?page={page}&pageSize={pageSize}", null, ct);

    /// <summary>Loads the messages of one of the user's conversations.</summary>
    /// <param name="id">Conversation id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the response has been relayed.</returns>
    [HttpGet("conversations/{id:guid}/messages")]
    public Task Messages(Guid id, CancellationToken ct) =>
        RelayAsync("GET", $"conversations/{id}/messages", null, ct);

    /// <summary>Deletes one of the user's conversations.</summary>
    /// <param name="id">Conversation id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the response has been relayed.</returns>
    [HttpDelete("conversations/{id:guid}")]
    public Task DeleteConversation(Guid id, CancellationToken ct) =>
        RelayAsync("DELETE", $"conversations/{id}", null, ct);

    /// <summary>Rates one of NMate's answers.</summary>
    /// <param name="id">Answer message id.</param>
    /// <param name="request">Rating and optional comment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the response has been relayed.</returns>
    [HttpPut("messages/{id:guid}/feedback")]
    public Task Feedback(Guid id, [FromBody] NMateFeedbackRequest request, CancellationToken ct) =>
        RelayAsync("PUT", $"messages/{id}/feedback", request, ct);

    /// <summary>Starter questions for the screen the user is on.</summary>
    /// <param name="route">Current SPA route.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the response has been relayed.</returns>
    [HttpGet("suggestions")]
    public Task Suggestions([FromQuery] string? route, CancellationToken ct) =>
        RelayAsync("GET", $"suggestions?route={Uri.EscapeDataString(route ?? string.Empty)}", null, ct);

    /// <summary>Whether NMate can answer right now (the widget shows a maintenance state otherwise).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the response has been relayed.</returns>
    [HttpGet("status")]
    public Task Status(CancellationToken ct) => RelayAsync("GET", "status", null, ct);

    private async Task RelayAsync(string method, string pathAndQuery, object? body, CancellationToken ct)
    {
        if (!gateway.IsEnabled)
        {
            // Not configured in this environment: behave as if the feature does not exist, so the widget hides.
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (currentUser.UserId is not { } userId || currentUser.OrgId is not { } orgId)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var json = body is null ? null : JsonSerializer.Serialize(body, body.GetType(), Json);
        await using var upstream = await gateway.SendAsync(new NMateUpstreamRequest(method, pathAndQuery, json, userId, orgId), ct);

        Response.StatusCode = upstream.StatusCode;
        if (upstream.ContentType is not null) Response.ContentType = upstream.ContentType;

        if (!upstream.IsEventStream)
        {
            await upstream.Body.CopyToAsync(Response.Body, ct);
            return;
        }

        // Stream: never cache, ask nginx (dashboard-web) not to buffer, and flush every piece immediately.
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var buffer = new byte[4096];
        int read;
        while ((read = await upstream.Body.ReadAsync(buffer, ct)) > 0)
        {
            await Response.Body.WriteAsync(buffer.AsMemory(0, read), ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}
