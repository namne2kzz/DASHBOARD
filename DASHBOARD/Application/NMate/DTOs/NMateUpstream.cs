namespace DASHBOARD.Application.NMate.DTOs;

/// <summary>A call DASHBOARD forwards to NMate on behalf of the signed-in user.</summary>
/// <param name="Method">HTTP method (<c>GET</c>, <c>POST</c>, <c>PUT</c>, <c>DELETE</c>).</param>
/// <param name="PathAndQuery">NMate path relative to <c>/internal/v1/</c>, e.g. <c>conversations?page=1</c>.</param>
/// <param name="JsonBody">Request body as JSON, or null.</param>
/// <param name="UserId">Authenticated user — forwarded as <c>X-User-Id</c>, never taken from the client.</param>
/// <param name="OrgId">User's organization — forwarded as <c>X-Org-Id</c>.</param>
public sealed record NMateUpstreamRequest(string Method, string PathAndQuery, string? JsonBody, Guid UserId, Guid OrgId);

/// <summary>NMate's response, with the body left open so a streamed answer can be relayed as it arrives.</summary>
/// <param name="StatusCode">HTTP status from NMate (or 503 when NMate could not be reached).</param>
/// <param name="ContentType">Response content type, if any.</param>
/// <param name="Body">Response body stream; read it to the end, then dispose the response.</param>
/// <param name="Owner">Underlying response that owns <paramref name="Body"/>.</param>
public sealed record NMateUpstreamResponse(int StatusCode, string? ContentType, Stream Body, IDisposable? Owner) : IAsyncDisposable
{
    /// <summary>Whether the body is a server-sent event stream that must be flushed piece by piece.</summary>
    public bool IsEventStream => ContentType?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) == true;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Body.DisposeAsync();
        Owner?.Dispose();
    }
}
