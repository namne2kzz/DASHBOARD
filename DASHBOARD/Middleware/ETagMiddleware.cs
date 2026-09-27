using System.Security.Cryptography;
using Microsoft.Net.Http.Headers;

namespace DASHBOARD.Middleware;

/// <summary>
/// Stamps successful <c>GET /api/*</c> responses with a weak ETag and answers <c>304 Not Modified</c>
/// when the client's <c>If-None-Match</c> already matches, so unchanged payloads are never re-sent.
/// </summary>
/// <remarks>
/// The response body must be buffered to hash it, so this only wraps JSON API reads — never
/// downloads, streams or non-GET verbs. Buffering caps out at <see cref="MaxBufferBytes"/>; a larger
/// response is streamed through untagged rather than held in memory.
/// </remarks>
/// <param name="next">Next middleware delegate.</param>
public sealed class ETagMiddleware(RequestDelegate next)
{
    /// <summary>Responses larger than this are passed through without an ETag.</summary>
    private const int MaxBufferBytes = 512 * 1024;

    /// <summary>Hashes the buffered response, tags it, and short-circuits to 304 on a match.</summary>
    /// <param name="context">Current HTTP context.</param>
    /// <returns>A task that completes once the response has been written.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsTaggable(context.Request))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        // Only a plain 200 with a JSON body is safe to tag: a 304 is meaningless for anything else,
        // and a response carrying Set-Cookie must never be reused from a client cache.
        var taggable = context.Response.StatusCode == StatusCodes.Status200OK
                       && buffer.Length is > 0 and <= MaxBufferBytes
                       && !context.Response.Headers.ContainsKey(HeaderNames.SetCookie)
                       && (context.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false);

        if (taggable)
        {
            var etag = ComputeETag(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
            context.Response.Headers.ETag = etag;

            if (MatchesIfNoneMatch(context.Request, etag))
            {
                // 304 carries no body: drop Content-Length so Kestrel does not promise bytes it
                // will not write.
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                context.Response.ContentLength = null;
                return;
            }
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody, context.RequestAborted);
    }

    /// <summary>Determines whether a request is a JSON API read worth buffering and tagging.</summary>
    /// <param name="request">Incoming request.</param>
    /// <returns><see langword="true"/> when the request is a GET under <c>/api</c>.</returns>
    private static bool IsTaggable(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) && request.Path.StartsWithSegments("/api");

    /// <summary>Builds a weak ETag from the response bytes.</summary>
    /// <param name="body">Buffered response body.</param>
    /// <returns>Quoted weak ETag value.</returns>
    private static string ComputeETag(ReadOnlySpan<byte> body)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(body, hash);
        return $"W/\"{Convert.ToHexStringLower(hash)[..32]}\"";
    }

    /// <summary>Checks the request's <c>If-None-Match</c> against the computed tag.</summary>
    /// <param name="request">Incoming request.</param>
    /// <param name="etag">ETag computed for the current response.</param>
    /// <returns><see langword="true"/> when the client already holds this version.</returns>
    private static bool MatchesIfNoneMatch(HttpRequest request, string etag)
    {
        // Browsers may send a comma-separated list, and a revalidating client can send "*".
        foreach (var header in request.Headers.IfNoneMatch)
        {
            if (string.IsNullOrEmpty(header)) continue;

            foreach (var candidate in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (candidate == "*" || string.Equals(candidate, etag, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }
}
