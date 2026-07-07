namespace DASHBOARD.Application.Common.Exceptions;

/// <summary>
/// Thrown when a Git provider (e.g. GitHub) rejects the configured Personal Access Token, or the
/// token lacks access to the configured repository (including the repository not existing from the
/// token's point of view, which GitHub's API also reports as 404 to avoid leaking existence of private repos).
/// </summary>
public sealed class GitCredentialInvalidException : Exception
{
    /// <summary>Initializes a new <see cref="GitCredentialInvalidException"/>.</summary>
    /// <param name="message">A description of why the credential was rejected.</param>
    /// <param name="innerException">The underlying exception thrown by the Git provider client, if any.</param>
    public GitCredentialInvalidException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
