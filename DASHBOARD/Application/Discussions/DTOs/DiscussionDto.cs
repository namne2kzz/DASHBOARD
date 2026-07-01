namespace DASHBOARD.Application.Discussions.DTOs;

/// <summary>A single discussion comment on a sprint task (work item).</summary>
public sealed record DiscussionDto(
    Guid      Id,
    Guid      SprintTaskId,
    Guid      AuthorId,
    string    AuthorName,
    string    AuthorAvatar,
    string    Body,
    DateTime  CreatedAt,
    DateTime? UpdatedAt);
