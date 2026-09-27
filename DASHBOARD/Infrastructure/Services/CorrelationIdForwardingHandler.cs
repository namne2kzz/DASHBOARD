using Microsoft.AspNetCore.Http;

namespace DASHBOARD.Infrastructure.Services;

/// <summary>
/// Copies the current request's <c>X-Correlation-Id</c> onto every outbound HTTP call so the
/// downstream service (HUB Chat) logs under the same id as this request.
/// </summary>
/// <param name="accessor">Accessor for the ambient HTTP context.</param>
public sealed class CorrelationIdForwardingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    private const string HeaderName = "X-Correlation-Id";
    private const string ItemsKey = "CorrelationId";

    /// <summary>Adds the correlation header, when one is in scope, and forwards the request.</summary>
    /// <param name="request">The outbound request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The downstream response.</returns>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        // Absent outside a request (MassTransit consumers, startup work) — nothing to forward.
        if (accessor.HttpContext?.Items.TryGetValue(ItemsKey, out var value) == true
            && value is string correlationId
            && !request.Headers.Contains(HeaderName))
        {
            request.Headers.TryAddWithoutValidation(HeaderName, correlationId);
        }

        return base.SendAsync(request, ct);
    }
}
