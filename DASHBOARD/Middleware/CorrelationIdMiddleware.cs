using Serilog.Context;

namespace DASHBOARD.Middleware;

/// <summary>
/// Reads the inbound <c>X-Correlation-Id</c> header (or generates one), echoes it back on the
/// response, and pushes it into the Serilog <see cref="LogContext"/> so every log event written
/// while handling the request carries the same id.
/// </summary>
/// <remarks>
/// A single user action can span DASHBOARD → HUB Chat → back again. Without a shared id the log
/// lines of those processes cannot be stitched together after the fact. Callers that originate a
/// request (the Angular SPA) generate the id; callers that forward one (HUB's DashboardGateway)
/// pass it through, so the whole chain shares one value.
/// </remarks>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    /// <summary>Header carrying the correlation id, both inbound and outbound.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>The <see cref="HttpContext.Items"/> key under which the id is stored for downstream code.</summary>
    public const string ItemsKey = "CorrelationId";

    /// <summary>Resolves the correlation id and invokes the rest of the pipeline within its logging scope.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Resolve(context);

        // Store for outbound HttpClient handlers (see CorrelationIdForwardingHandler) and for
        // error responses that want to quote the id back to the user.
        context.Items[ItemsKey] = correlationId;

        // OnStarting, not a direct assignment: headers cannot be written once the response has
        // begun, and a downstream middleware may start it before we unwind.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(ItemsKey, correlationId))
            await next(context);
    }

    /// <summary>
    /// Takes the inbound header when present and plausible, otherwise falls back to the
    /// ASP.NET Core trace identifier.
    /// </summary>
    private static string Resolve(HttpContext context)
    {
        var inbound = context.Request.Headers[HeaderName].FirstOrDefault();

        // The header is attacker-controlled and lands in log files, so treat it as untrusted:
        // cap the length and allow only characters that cannot forge a new log line or break
        // the JSON/text layout.
        if (!string.IsNullOrWhiteSpace(inbound) && inbound.Length <= 64 && IsSafe(inbound))
            return inbound;

        return context.TraceIdentifier;
    }

    private static bool IsSafe(string value)
    {
        foreach (var c in value)
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_' && c != ':' && c != '.')
                return false;

        return true;
    }
}
