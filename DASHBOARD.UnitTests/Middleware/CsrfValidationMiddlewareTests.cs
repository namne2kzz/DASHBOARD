using DASHBOARD.Core.Constants;
using DASHBOARD.Middleware;
using Microsoft.AspNetCore.Http;

namespace DASHBOARD.UnitTests.Middleware;

/// <summary>
/// Unit tests for <see cref="CsrfValidationMiddleware"/> — the double-submit guard on
/// cookie-authenticated writes.
/// </summary>
public sealed class CsrfValidationMiddlewareTests
{
    private const string Token = "4f8c2b1e9d7a4e3fb6c5a0d9e8f71234";

    /// <summary>Builds the middleware, recording whether the request was forwarded.</summary>
    /// <param name="onwards">Invoked when the middleware calls the next delegate.</param>
    private static CsrfValidationMiddleware Create(Action onwards) =>
        new(_ => { onwards(); return Task.CompletedTask; });

    /// <summary>
    /// A request shaped the way the guard cares about.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Request path.</param>
    /// <param name="accessCookie">Whether the ambient auth cookie is present.</param>
    /// <param name="cookieToken">Value of the readable CSRF cookie, if any.</param>
    /// <param name="headerToken">Value echoed in the CSRF header, if any.</param>
    /// <param name="authorization">An Authorization header value, if any.</param>
    private static DefaultHttpContext Request(
        string method,
        string path = "/api/v1/repositories",
        bool accessCookie = true,
        string? cookieToken = Token,
        string? headerToken = Token,
        string? authorization = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var cookies = new List<string>();
        if (accessCookie) cookies.Add($"{AppConstants.AccessTokenCookie}=jwt");
        if (cookieToken is not null) cookies.Add($"{AppConstants.CsrfCookie}={cookieToken}");
        if (cookies.Count > 0) context.Request.Headers.Cookie = string.Join("; ", cookies);

        if (headerToken is not null) context.Request.Headers[AppConstants.CsrfHeader] = headerToken;
        if (authorization is not null) context.Request.Headers.Authorization = authorization;

        return context;
    }

    // ── The pair itself ─────────────────────────────────────────────────────

    [Fact]
    public async Task MatchingPair_IsLetThrough()
    {
        var reached = false;
        var middleware = Create(() => reached = true);

        await middleware.InvokeAsync(Request("POST"));

        reached.Should().BeTrue();
    }

    [Fact]
    public async Task MismatchedPair_Is403()
    {
        var reached = false;
        var middleware = Create(() => reached = true);
        var context = Request("POST", headerToken: "a-different-token-entirely-abcd");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        reached.Should().BeFalse();
    }

    [Theory]
    // Differing in the first character versus the last is the shape a timing attack exploits: with
    // a short-circuiting comparison the second takes measurably longer to reject, and a caller who
    // can already make the browser send the cookie could read the secret back one character at a
    // time, then forge the header it was missing.
    [InlineData("Xf8c2b1e9d7a4e3fb6c5a0d9e8f71234")]
    [InlineData("4f8c2b1e9d7a4e3fb6c5a0d9e8f7123X")]
    public async Task NearMissesAreRejectedWhereverTheyDiffer(string headerToken)
    {
        var middleware = Create(() => { });
        var context = Request("POST", headerToken: headerToken);

        headerToken.Length.Should().Be(Token.Length, "a length mismatch is rejected before the comparison");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MissingHeader_Is403()
    {
        var middleware = Create(() => { });
        var context = Request("POST", headerToken: null);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MissingCookie_Is403()
    {
        // Two absent values must not count as equal, or the whole guard would be bypassed by
        // simply sending neither.
        var middleware = Create(() => { });
        var context = Request("POST", cookieToken: null, headerToken: null);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // ── Who the guard does not apply to ─────────────────────────────────────

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task SafeMethods_AreNotChecked(string method)
    {
        // A read changes nothing, so there is no cross-site write to prevent.
        var reached = false;
        var middleware = Create(() => reached = true);

        await middleware.InvokeAsync(Request(method, headerToken: null));

        reached.Should().BeTrue();
    }

    [Fact]
    public async Task BearerAuthenticatedWrite_IsNotChecked()
    {
        // A bearer caller is not relying on the ambient cookie, so a victim's browser cannot be
        // made to send its credential — CSRF has nothing to protect there.
        var reached = false;
        var middleware = Create(() => reached = true);

        await middleware.InvokeAsync(Request("POST", headerToken: null, authorization: "Bearer abc"));

        reached.Should().BeTrue();
    }

    [Fact]
    public async Task WriteWithoutTheAuthCookie_IsNotChecked()
    {
        var reached = false;
        var middleware = Create(() => reached = true);

        await middleware.InvokeAsync(Request("POST", accessCookie: false, headerToken: null));

        reached.Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/api/v1/auth/google-login")]
    [InlineData("/api/v1/auth/set-password")]
    [InlineData("/api/v1/auth/refresh")]
    public async Task EndpointsCarryingTheirOwnCredential_AreNotChecked(string path)
    {
        // These establish a session rather than consuming one. Without the exemption, somebody
        // already signed in could not open a password-setup link from their inbox.
        var reached = false;
        var middleware = Create(() => reached = true);

        await middleware.InvokeAsync(Request("POST", path, headerToken: null));

        reached.Should().BeTrue();
    }
}
