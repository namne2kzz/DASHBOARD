using System.Net.Http.Headers;
using System.Text;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.NMate.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DASHBOARD.Infrastructure.Services;

/// <summary>
/// Forwards calls to NMate's <c>/internal/v1/*</c> API through the named <c>"NMate"</c> HttpClient
/// (base URL, <c>X-Internal-Token</c> and correlation-id forwarding are configured in DI).
/// </summary>
/// <param name="factory">HTTP client factory.</param>
/// <param name="settings">App settings (to know whether NMate is configured).</param>
/// <param name="logger">Logger.</param>
public sealed class NMateGateway(
    IHttpClientFactory      factory,
    IAppSettings            settings,
    ILogger<NMateGateway>   logger) : INMateGateway
{
    /// <summary>Name of the HttpClient registered for NMate.</summary>
    public const string ClientName = "NMate";

    private const string UnavailableBody =
        """{"error":"NMATE_UNAVAILABLE","message":"NMate đang bảo trì, bạn thử lại sau nhé."}""";

    /// <inheritdoc />
    public bool IsEnabled => !string.IsNullOrWhiteSpace(settings.NMateBaseUrl);

    /// <inheritdoc />
    public async Task<NMateUpstreamResponse> SendAsync(NMateUpstreamRequest request, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(new HttpMethod(request.Method), "internal/v1/" + request.PathAndQuery);
        message.Headers.Add("X-User-Id", request.UserId.ToString());
        message.Headers.Add("X-Org-Id", request.OrgId.ToString());
        if (request.JsonBody is not null)
            message.Content = new StringContent(request.JsonBody, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            // ResponseHeadersRead: return as soon as headers arrive so a streamed answer is relayed
            // token by token instead of being buffered until NMate finishes.
            response = await factory.CreateClient(ClientName)
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "NMate unreachable for {Method} {Path}", request.Method, request.PathAndQuery);
            return Unavailable();
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient.Timeout surfaces as a cancellation that the caller did not ask for.
            logger.LogWarning(ex, "NMate timed out for {Method} {Path}", request.Method, request.PathAndQuery);
            return Unavailable();
        }

        var body = await response.Content.ReadAsStreamAsync(ct);
        return new NMateUpstreamResponse(
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.ToString(),
            body,
            response);
    }

    private static NMateUpstreamResponse Unavailable() => new(
        StatusCodes.Status503ServiceUnavailable,
        new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" }.ToString(),
        new MemoryStream(Encoding.UTF8.GetBytes(UnavailableBody)),
        Owner: null);
}
