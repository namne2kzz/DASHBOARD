using Microsoft.AspNetCore.Mvc.Filters;

namespace DASHBOARD.Core.Attributes;

/// <summary>
/// Opts a GET action into browser caching by writing a <c>private</c> <c>Cache-Control</c> header,
/// overriding the <c>no-store</c> default applied to every other read.
/// </summary>
/// <remarks>
/// <para>
/// Always <c>private</c>: every response here is scoped to the caller, so a shared proxy or CDN must
/// never hand one user's payload to another.
/// </para>
/// <para>
/// A <c>max-age</c> of zero still matters — it keeps the response storable while forcing revalidation,
/// which is exactly what <c>ETagMiddleware</c> needs to turn an unchanged payload into a 304. Without
/// any header at all the browser falls back to its own heuristics and may not store the body, leaving
/// nothing to revalidate against.
/// </para>
/// <para>
/// Only applied to successful responses: a 4xx/5xx body is never worth reusing from cache.
/// </para>
/// </remarks>
/// <param name="maxAgeSeconds">Freshness window in seconds; see <see cref="Core.Constants.CacheProfiles"/>.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ClientCacheAttribute(int maxAgeSeconds) : ActionFilterAttribute
{
    /// <summary>Writes the <c>Cache-Control</c> header once the action has produced a result.</summary>
    /// <param name="context">Context of the executed action.</param>
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        var status = context.HttpContext.Response.StatusCode;
        if (status is >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices)
        {
            context.HttpContext.Response.Headers.CacheControl =
                $"private, max-age={maxAgeSeconds}, must-revalidate";
        }

        base.OnResultExecuting(context);
    }
}
