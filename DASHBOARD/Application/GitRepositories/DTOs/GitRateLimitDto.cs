namespace DASHBOARD.Application.GitRepositories.DTOs;

/// <summary>The GitHub API rate-limit status for the token used on the current connection.</summary>
public sealed record GitRateLimitDto(
    int      Limit,
    int      Remaining,
    DateTime ResetAt);
