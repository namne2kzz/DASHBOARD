namespace DASHBOARD.Application.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated caller lacks the privilege a request requires.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="UnauthorizedAccessException"/>, which the pipeline maps to 401.
/// The distinction matters to clients: 401 says "your credentials are missing or stale, try signing
/// in again", while 403 says "we know who you are, and the answer is no". Mapping an authorisation
/// failure to 401 sends the browser off to refresh a token that was never the problem.
/// </remarks>
public sealed class ForbiddenException : Exception
{
    /// <summary>Initializes a new <see cref="ForbiddenException"/> with a caller-facing reason.</summary>
    /// <param name="message">Why the request was refused.</param>
    public ForbiddenException(string message) : base(message) { }
}
