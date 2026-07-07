namespace DASHBOARD.Application.GitRepositories.DTOs;

/// <summary>A single commit from a connected GitHub repository, with the linked work item resolved when its ID is referenced in the message.</summary>
public sealed record GitCommitDto(
    string   Sha,
    string   ShortSha,
    string   Branch,
    string   Message,
    string   AuthorName,
    string   AuthorEmail,
    DateTime Date,
    string?  LinkedWorkItemId);
