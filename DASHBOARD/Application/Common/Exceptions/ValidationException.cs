using FluentValidation.Results;

namespace DASHBOARD.Application.Common.Exceptions;

/// <summary>Thrown when one or more FluentValidation validators fail in the MediatR pipeline.</summary>
public sealed class ValidationException : Exception
{
    /// <summary>Gets the validation errors grouped by property name.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Initializes a new <see cref="ValidationException"/> from a list of FluentValidation failures.</summary>
    /// <param name="failures">The validation failures to aggregate into the Errors dictionary.</param>
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation failures occurred.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }
}
