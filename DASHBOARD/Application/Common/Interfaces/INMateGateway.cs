using DASHBOARD.Application.NMate.DTOs;

namespace DASHBOARD.Application.Common.Interfaces;

/// <summary>
/// Forwards calls to the NMate service (separate repo <c>SUPPORT</c>), which lives only on the internal network.
/// DASHBOARD authenticates the user and supplies their identity; NMate owns all AI logic.
/// </summary>
public interface INMateGateway
{
    /// <summary>Whether NMate is configured (<c>NMate:BaseUrl</c> set).</summary>
    bool IsEnabled { get; }

    /// <summary>Sends one call to NMate and returns as soon as the response headers arrive.</summary>
    /// <param name="request">What to forward and for whom.</param>
    /// <param name="ct">Cancellation token; cancelling aborts the upstream call (and stops answer generation).</param>
    /// <returns>
    /// NMate's response with an open body. When NMate is unreachable or times out, a synthetic
    /// <c>503 { error: "NMATE_UNAVAILABLE" }</c> response — never an exception — so the widget shows a maintenance state.
    /// </returns>
    Task<NMateUpstreamResponse> SendAsync(NMateUpstreamRequest request, CancellationToken ct);
}
