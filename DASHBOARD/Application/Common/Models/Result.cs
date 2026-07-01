namespace DASHBOARD.Application.Common.Models;

/// <summary>Discriminated union representing either a successful value or a failure message. Use for business-rule errors — never for infrastructure exceptions.</summary>
/// <typeparam name="T">The success value type.</typeparam>
public sealed class Result<T>
{
    /// <summary>Gets the success value; <c>default</c> if this is a failure.</summary>
    public T? Value { get; }

    /// <summary>Gets the error message; <c>null</c> if this is a success.</summary>
    public string? Error { get; }

    /// <summary>Gets whether this result represents a successful outcome.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Gets whether this result represents a failed outcome.</summary>
    public bool IsFailure => !IsSuccess;

    private Result(T value) => Value = value;
    private Result(string error) => Error = error;

    /// <summary>Creates a successful result wrapping the given value.</summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful <see cref="Result{T}"/>.</returns>
    public static Result<T> Success(T value) => new(value);

    /// <summary>Creates a failed result with the given error message.</summary>
    /// <param name="error">The human-readable error message.</param>
    /// <returns>A failed <see cref="Result{T}"/>.</returns>
    public static Result<T> Failure(string error) => new(error);
}

/// <summary>Non-generic result for operations that succeed with no return value.</summary>
public sealed class Result
{
    /// <summary>Gets whether this result represents a successful outcome.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets whether this result represents a failed outcome.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Gets the error message; <c>null</c> if this is a success.</summary>
    public string? Error { get; }

    private Result(bool success, string? error) { IsSuccess = success; Error = error; }

    /// <summary>A singleton successful result with no value.</summary>
    public static readonly Result Ok = new(true, null);

    /// <summary>Creates a failed result with the given error message.</summary>
    /// <param name="error">The human-readable error message.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    public static Result Failure(string error) => new(false, error);
}
