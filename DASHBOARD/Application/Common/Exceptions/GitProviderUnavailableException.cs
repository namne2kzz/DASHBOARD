namespace DASHBOARD.Application.Common.Exceptions;

/// <summary>
/// Thrown when a call to an external Git provider (e.g. GitHub) fails for a reason other than
/// invalid credentials or rate limiting — network failure, provider outage, unexpected API error, etc.
/// </summary>
public sealed class GitProviderUnavailableException : Exception
{
    /// <summary>Initializes a new <see cref="GitProviderUnavailableException"/>.</summary>
    /// <param name="message">A description of the provider failure.</param>
    /// <param name="innerException">The underlying exception thrown by the Git provider client, if any.</param>
    public GitProviderUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
