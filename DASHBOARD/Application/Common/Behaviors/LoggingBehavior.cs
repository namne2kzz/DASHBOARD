using MediatR;
using Microsoft.Extensions.Logging;

namespace DASHBOARD.Application.Common.Behaviors;

/// <summary>MediatR pipeline behavior that logs request entry, completion, and emits a warning for slow requests.</summary>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The MediatR response type.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 500;

    /// <summary>Logs entry and exit for each request; emits a warning when elapsed time exceeds <see cref="SlowRequestThresholdMs"/>.</summary>
    /// <param name="request">The incoming MediatR request.</param>
    /// <param name="next">Delegate to invoke the next behavior or handler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response from the next handler in the pipeline.</returns>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}", name);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await next(ct);
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
            logger.LogWarning("Slow request — {RequestName} took {ElapsedMs}ms", name, sw.ElapsedMilliseconds);
        else
            logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms", name, sw.ElapsedMilliseconds);

        return response;
    }
}
