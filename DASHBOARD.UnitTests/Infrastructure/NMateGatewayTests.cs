using System.Net;
using System.Text;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.NMate.DTOs;
using DASHBOARD.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DASHBOARD.UnitTests.Infrastructure;

/// <summary>Unit tests for <see cref="NMateGateway"/>.</summary>
public sealed class NMateGatewayTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrgId  = Guid.NewGuid();

    /// <summary>The call goes under /internal/v1/ with the user's identity headers and JSON body.</summary>
    [Fact]
    public async Task SendAsync_ForwardsPathIdentityAndBody()
    {
        HttpRequestMessage? sent = null;
        string? sentBody = null;
        var gateway = CreateGateway(async request =>
        {
            sent     = request;
            sentBody = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, """{"ok":true}""");
        });

        await using var response = await gateway.SendAsync(
            new NMateUpstreamRequest("PUT", "messages/abc/feedback", """{"rating":1}""", UserId, OrgId), CancellationToken.None);

        sent!.Method.Should().Be(HttpMethod.Put);
        sent.RequestUri!.ToString().Should().Be("http://nmate.test/internal/v1/messages/abc/feedback");
        sent.Headers.GetValues("X-User-Id").Should().ContainSingle(UserId.ToString());
        sent.Headers.GetValues("X-Org-Id").Should().ContainSingle(OrgId.ToString());
        sentBody.Should().Be("""{"rating":1}""");

        response.StatusCode.Should().Be(200);
        response.ContentType.Should().StartWith("application/json");
        (await new StreamReader(response.Body).ReadToEndAsync()).Should().Be("""{"ok":true}""");
    }

    /// <summary>NMate's own error statuses are relayed untouched (the widget reads its error codes).</summary>
    [Fact]
    public async Task SendAsync_UpstreamError_IsRelayedAsIs()
    {
        var gateway = CreateGateway(_ => Task.FromResult(Json(HttpStatusCode.Conflict, """{"error":"NMATE_STREAM_IN_PROGRESS"}""")));

        await using var response = await gateway.SendAsync(Get("status"), CancellationToken.None);

        response.StatusCode.Should().Be(409);
    }

    /// <summary>NMate down → a 503 the widget understands, never an exception that becomes a 500.</summary>
    [Fact]
    public async Task SendAsync_Unreachable_Returns503Unavailable()
    {
        var gateway = CreateGateway(_ => throw new HttpRequestException("connection refused"));

        await using var response = await gateway.SendAsync(Get("status"), CancellationToken.None);

        response.StatusCode.Should().Be(503);
        (await new StreamReader(response.Body).ReadToEndAsync()).Should().Contain("NMATE_UNAVAILABLE");
    }

    /// <summary>HttpClient.Timeout surfaces as a cancellation nobody asked for — also a 503.</summary>
    [Fact]
    public async Task SendAsync_Timeout_Returns503Unavailable()
    {
        var gateway = CreateGateway(_ => throw new TaskCanceledException("timeout"));

        await using var response = await gateway.SendAsync(Get("status"), CancellationToken.None);

        response.StatusCode.Should().Be(503);
    }

    /// <summary>The user closing the widget is a real cancellation and must propagate (it stops generation upstream).</summary>
    [Fact]
    public async Task SendAsync_CallerCancels_Propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var gateway = CreateGateway(_ => throw new TaskCanceledException());

        var act = () => gateway.SendAsync(Get("status"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>Without a base URL the feature is off.</summary>
    [Theory]
    [InlineData("", false)]
    [InlineData("http://localhost:5160", true)]
    public void IsEnabled_FollowsBaseUrl(string baseUrl, bool expected)
    {
        CreateGateway(_ => throw new InvalidOperationException(), baseUrl).IsEnabled.Should().Be(expected);
    }

    private static NMateUpstreamRequest Get(string path) => new("GET", path, null, UserId, OrgId);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static NMateGateway CreateGateway(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle, string baseUrl = "http://nmate.test")
    {
        var client  = new HttpClient(new StubHandler(handle)) { BaseAddress = new Uri("http://nmate.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(NMateGateway.ClientName)).Returns(client);

        var settings = new Mock<IAppSettings>();
        settings.SetupGet(s => s.NMateBaseUrl).Returns(baseUrl);

        return new NMateGateway(factory.Object, settings.Object, NullLogger<NMateGateway>.Instance);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return handle(request);
        }
    }
}
