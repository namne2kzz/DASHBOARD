using System.Text;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.NMate.DTOs;
using DASHBOARD.Controllers.NMate;
using DASHBOARD.Controllers.NMate.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DASHBOARD.UnitTests.Controllers;

/// <summary>Unit tests for <see cref="NMateController"/> — the authenticated relay to NMate.</summary>
public sealed class NMateControllerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrgId  = Guid.NewGuid();

    private readonly Mock<INMateGateway>       _gateway     = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly CountingStream            _responseBody = new();
    private NMateUpstreamRequest?              _forwarded;

    /// <summary>Signed-in user, NMate enabled, gateway records what it was asked.</summary>
    public NMateControllerTests()
    {
        _currentUser.SetupGet(u => u.UserId).Returns(UserId);
        _currentUser.SetupGet(u => u.OrgId).Returns(OrgId);
        _gateway.SetupGet(g => g.IsEnabled).Returns(true);
    }

    /// <summary>NMate not configured → 404, so the widget hides itself; nothing is forwarded.</summary>
    [Fact]
    public async Task Disabled_Returns404WithoutCallingNMate()
    {
        _gateway.SetupGet(g => g.IsEnabled).Returns(false);
        var (controller, context) = CreateController();

        await controller.Status(CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        _gateway.Verify(g => g.SendAsync(It.IsAny<NMateUpstreamRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Without a resolved user/org there is no identity to forward.</summary>
    [Fact]
    public async Task NoUserIdentity_Returns401()
    {
        _currentUser.SetupGet(u => u.OrgId).Returns((Guid?)null);
        var (controller, context) = CreateController();

        await controller.Status(CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    /// <summary>JSON responses are copied through with NMate's status; identity comes from the token, body is camelCase JSON.</summary>
    [Fact]
    public async Task Feedback_RelaysJsonWithIdentityAndCamelCaseBody()
    {
        Upstream(204, null, "");
        var (controller, context) = CreateController();
        var messageId = Guid.NewGuid();

        await controller.Feedback(messageId, new NMateFeedbackRequest(-1, "Sai rồi"), CancellationToken.None);

        context.Response.StatusCode.Should().Be(204);
        _forwarded!.Method.Should().Be("PUT");
        _forwarded.PathAndQuery.Should().Be($"messages/{messageId}/feedback");
        _forwarded.UserId.Should().Be(UserId);
        _forwarded.OrgId.Should().Be(OrgId);
        _forwarded.JsonBody.Should().Be("""{"rating":-1,"comment":"Sai rồi"}""");
    }

    /// <summary>A streamed answer is relayed byte for byte, flushed per piece, with no-cache/no-buffer headers.</summary>
    [Fact]
    public async Task Chat_RelaysEventStreamAndFlushesEachPiece()
    {
        const string sse = "event: meta\ndata: {}\n\nevent: delta\ndata: {\"text\":\"Bấm \"}\n\nevent: done\ndata: {}\n\n";
        Upstream(200, "text/event-stream; charset=utf-8", sse, readSize: 16);
        var (controller, context) = CreateController();

        await controller.Chat(new AskNMateRequest(null, "Làm sao đóng sprint?", new AskNMateContext("/acme/DASH/sprint-planning", null)), CancellationToken.None);

        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().StartWith("text/event-stream");
        context.Response.Headers.CacheControl.ToString().Should().Be("no-cache");
        context.Response.Headers["X-Accel-Buffering"].ToString().Should().Be("no");
        Encoding.UTF8.GetString(_responseBody.ToArray()).Should().Be(sse);
        _responseBody.Flushes.Should().BeGreaterThan(1, "each piece must reach the browser as soon as it arrives");

        _forwarded!.PathAndQuery.Should().Be("chat");
        _forwarded.JsonBody.Should().Contain("\"message\":\"Làm sao đóng sprint?\"").And.Contain("\"route\":\"/acme/DASH/sprint-planning\"");
    }

    /// <summary>Query values are escaped so a crafted route cannot add parameters to the upstream call.</summary>
    [Fact]
    public async Task Suggestions_EscapesRoute()
    {
        Upstream(200, "application/json", "[]");
        var (controller, _) = CreateController();

        await controller.Suggestions("/a?x=1&page=9", CancellationToken.None);

        _forwarded!.PathAndQuery.Should().Be("suggestions?route=%2Fa%3Fx%3D1%26page%3D9");
    }

    private (NMateController Controller, HttpContext Context) CreateController()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = _responseBody;
        var controller = new NMateController(_gateway.Object, _currentUser.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };
        return (controller, context);
    }

    private void Upstream(int status, string? contentType, string body, int readSize = 4096) =>
        _gateway.Setup(g => g.SendAsync(It.IsAny<NMateUpstreamRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NMateUpstreamRequest, CancellationToken>((r, _) => _forwarded = r)
            .ReturnsAsync(() => new NMateUpstreamResponse(status, contentType, new SmallReadStream(Encoding.UTF8.GetBytes(body), readSize), null));

    /// <summary>Response body that counts flushes.</summary>
    private sealed class CountingStream : MemoryStream
    {
        public int Flushes { get; private set; }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Flushes++;
            return base.FlushAsync(cancellationToken);
        }
    }

    /// <summary>Upstream body that hands out at most <c>readSize</c> bytes per read, like a network stream.</summary>
    private sealed class SmallReadStream(byte[] data, int readSize) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, readSize)], cancellationToken);
    }
}
