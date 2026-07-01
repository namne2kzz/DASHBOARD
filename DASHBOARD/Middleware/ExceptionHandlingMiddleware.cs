using System.Net;
using System.Text.Json;
using DASHBOARD.Application.Common.Exceptions;

namespace DASHBOARD.Middleware;

/// <summary>Global middleware that catches unhandled exceptions and converts them to structured JSON error responses.</summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Invokes the next middleware and handles any exceptions that propagate up.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, title, errors) = exception switch
        {
            ValidationException ve => (
                HttpStatusCode.UnprocessableEntity,
                "One or more validation errors occurred.",
                (object)ve.Errors),

            NotFoundException nfe => (
                HttpStatusCode.NotFound,
                nfe.Message,
                (object)new Dictionary<string, string[]>()),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized.",
                (object)new Dictionary<string, string[]>()),

            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred.",
                (object)new Dictionary<string, string[]>())
        };

        if (exception is not (ValidationException or NotFoundException))
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = (int)status,
            title,
            errors,
        }, JsonOptions));
    }
}
