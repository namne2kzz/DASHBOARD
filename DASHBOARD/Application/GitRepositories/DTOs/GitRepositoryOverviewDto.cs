namespace DASHBOARD.Application.GitRepositories.DTOs;

/// <summary>
/// The GitHub overview for a project's (currently selected) connected repository.
/// When <see cref="HasConnection"/> is <c>false</c>, every other field is empty/null and the
/// frontend renders the "not connected" empty state.
/// </summary>
public sealed record GitRepositoryOverviewDto(
    bool                             HasConnection,
    string?                          RepoUrl,
    string?                          FullName,
    string?                          DefaultBranch,
    IReadOnlyList<GitBranchDto>      Branches,
    IReadOnlyList<GitCommitDto>      Commits,
    IReadOnlyList<GitPullRequestDto> PullRequests,
    GitRateLimitDto?                 RateLimit,
    string?                          Status,
    string?                          LastSyncError);
