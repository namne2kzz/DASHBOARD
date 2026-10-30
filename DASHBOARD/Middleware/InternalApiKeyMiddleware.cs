using System.Security.Cryptography;
using System.Text;

namespace DASHBOARD.Middleware;

/// <summary>
/// Protects <c>/internal/*</c> routes with a shared static token (header <c>X-Internal-Token</c>).
/// Only HUB services (running inside the same Docker network) should know this secret.
/// </summary>
/// <param name="next">Next middleware delegate.</param>
/// <param name="config">Application configuration.</param>
public sealed class InternalApiKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    private const string Header = "X-Internal-Token";

    /// <summary>Validates the internal token before forwarding the request.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/internal"))
        {
            var expected = config["InternalApi:Token"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("InternalApi:Token not configured.");
                return;
            }

            context.Request.Headers.TryGetValue(Header, out var provided);
            if (!Matches(provided.ToString(), expected))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid or missing X-Internal-Token.");
                return;
            }
        }

        await next(context);
    }

    /// <summary>
    /// Compares the supplied token with the configured one without leaking how much of it matched.
    /// </summary>
    /// <remarks>
    /// <see cref="string.Equals(string?, string?, StringComparison)"/> returns at the first byte that
    /// differs, so the time it takes reveals the length of the matching prefix — enough to recover a
    /// shared secret one character at a time over many requests. <see cref="CryptographicOperations.FixedTimeEquals"/>
    /// always reads both buffers to the end. The length check in front of it is not a leak: the
    /// length of a configured token is not the secret, and the SUPPORT service's equivalent guard
    /// does the same.
    /// </remarks>
    /// <param name="provided">Token from the request header; empty when the header is absent.</param>
    /// <param name="expected">Token from configuration.</param>
    /// <returns>True when the two are identical.</returns>
    private static bool Matches(string provided, string expected)
    {
        if (provided.Length == 0) return false;

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
