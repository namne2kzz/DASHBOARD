using DASHBOARD.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace DASHBOARD.UnitTests.Middleware;

/// <summary>Unit tests for <see cref="InternalApiKeyMiddleware"/> — the guard on <c>/internal/*</c>.</summary>
public sealed class InternalApiKeyMiddlewareTests
{
    private const string Token = "SECRET_FOR_USE_INTERNAL_API_CALLS_ONLY_WITH32!@$";

    /// <summary>Builds the middleware over a configuration holding the given expected token.</summary>
    /// <param name="expected">The configured token, or null to leave it unset.</param>
    /// <param name="onwards">Set to true by the middleware calling the next delegate.</param>
    private static InternalApiKeyMiddleware Create(string? expected, Action onwards)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(expected is null
                ? []
                : new Dictionary<string, string?> { ["InternalApi:Token"] = expected })
            .Build();

        return new InternalApiKeyMiddleware(_ => { onwards(); return Task.CompletedTask; }, config);
    }

    /// <summary>A request to the given path, optionally carrying a token header.</summary>
    private static DefaultHttpContext Request(string path, string? token = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (token is not null) context.Request.Headers["X-Internal-Token"] = token;
        return context;
    }

    [Fact]
    public async Task NonInternalPath_IsNotGuarded()
    {
        var reached = false;
        var middleware = Create(Token, () => reached = true);

        await middleware.InvokeAsync(Request("/api/v1/repositories"));

        reached.Should().BeTrue("the guard only covers /internal/*");
    }

    [Fact]
    public async Task CorrectToken_IsLetThrough()
    {
        var reached = false;
        var middleware = Create(Token, () => reached = true);

        await middleware.InvokeAsync(Request("/internal/v1/users", Token));

        reached.Should().BeTrue();
    }

    [Fact]
    public async Task WrongToken_Is401()
    {
        var reached = false;
        var middleware = Create(Token, () => reached = true);
        var context = Request("/internal/v1/users", "not-the-token");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        reached.Should().BeFalse();
    }

    [Fact]
    public async Task MissingHeader_Is401()
    {
        var reached = false;
        var middleware = Create(Token, () => reached = true);
        var context = Request("/internal/v1/users");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        reached.Should().BeFalse();
    }

    [Fact]
    public async Task UnconfiguredToken_Is503RatherThanLettingEverybodyIn()
    {
        // An empty expected token must never mean "anything matches" — that would leave /internal/*
        // wide open on a deployment that simply forgot to set it.
        var reached = false;
        var middleware = Create(null, () => reached = true);
        var context = Request("/internal/v1/users", "anything");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        reached.Should().BeFalse();
    }

    [Fact]
    public async Task EmptyHeaderAgainstConfiguredToken_Is401()
    {
        var middleware = Create(Token, () => { });
        var context = Request("/internal/v1/users", string.Empty);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task TokenOfTheSameLengthButDifferent_Is401()
    {
        // FixedTimeEquals requires equal-length buffers to compare at all; a same-length near-miss
        // is the case that actually exercises the comparison rather than the length check.
        var nearMiss = Token[..^1] + "?";
        var middleware = Create(Token, () => { });
        var context = Request("/internal/v1/users", nearMiss);

        nearMiss.Length.Should().Be(Token.Length);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Theory]
    // Differing in the first byte versus the last is the shape a timing attack exploits: with a
    // short-circuiting comparison the second takes measurably longer to reject, which is how a
    // secret gets recovered one character at a time. Both must be refused identically.
    [InlineData("?SECRET_FOR_USE_INTERNAL_API_CALLS_ONLY_WITH32!@")]
    [InlineData("SECRET_FOR_USE_INTERNAL_API_CALLS_ONLY_WITH32!@?")]
    public async Task NearMissesAreRejectedWhereverTheyDiffer(string provided)
    {
        var middleware = Create(Token, () => { });
        var context = Request("/internal/v1/users", provided);

        provided.Length.Should().Be(Token.Length, "a length mismatch would be rejected before the comparison");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}
