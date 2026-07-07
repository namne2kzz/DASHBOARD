namespace DASHBOARD.Application.Common.Exceptions;

/// <summary>Thrown when the Git provider's (e.g. GitHub) API rate limit has been exhausted for the configured token.</summary>
public sealed class GitRateLimitExceededException : Exception
{
    /// <summary>Gets the UTC instant at which the rate limit resets, if known.</summary>
    public DateTimeOffset? ResetAt { get; }

    /// <summary>Initializes a new <see cref="GitRateLimitExceededException"/>.</summary>
    /// <param name="resetAt">The UTC instant at which the rate limit resets, if known.</param>
    /// <param name="innerException">The underlying exception thrown by the Git provider client, if any.</param>
    public GitRateLimitExceededException(DateTimeOffset? resetAt, Exception? innerException = null)
        : base(resetAt is { } reset
            ? $"GitHub API rate limit exceeded. Resets at {reset:O}."
            : "GitHub API rate limit exceeded.", innerException)
    {
        ResetAt = resetAt;
    }
}
