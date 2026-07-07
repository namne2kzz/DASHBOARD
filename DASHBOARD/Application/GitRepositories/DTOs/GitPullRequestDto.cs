namespace DASHBOARD.Application.GitRepositories.DTOs;

/// <summary>A single pull request from a connected GitHub repository, with the linked work item resolved when its ID is referenced in the title.</summary>
public sealed record GitPullRequestDto(
    int                      Number,
    string                   Title,
    string                   SourceBranch,
    string                   TargetBranch,
    string                   Status,
    string                   ReviewDecision,
    string                   AuthorLogin,
    IReadOnlyList<string>    Reviewers,
    string?                  LinkedWorkItemId,
    DateTime?                MergedAt);
