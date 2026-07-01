using FluentValidation;
using MediatR;

namespace DASHBOARD.Application.Common.Behaviors;

/// <summary>MediatR pipeline behavior that runs all registered FluentValidation validators before the handler executes.</summary>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The MediatR response type.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Validates the request; throws <see cref="Exceptions.ValidationException"/> on failure, otherwise calls the next handler.</summary>
    /// <param name="request">The incoming MediatR request.</param>
    /// <param name="next">Delegate to invoke the next behavior or handler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response from the next handler in the pipeline.</returns>
    /// <exception cref="Exceptions.ValidationException">Thrown when any validator reports failures.</exception>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await next(ct);

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, ct))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new Exceptions.ValidationException(failures);

        return await next(ct);
    }
}
