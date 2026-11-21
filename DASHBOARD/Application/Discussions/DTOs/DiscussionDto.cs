namespace DASHBOARD.Application.Discussions.DTOs;

/// <summary>A single discussion comment on a sprint task (work item).</summary>
/// <param name="Id">The comment id.</param>
/// <param name="SprintTaskId">The sprint task this comment belongs to.</param>
/// <param name="ParentId">The root comment this is a reply to; null for a root comment.</param>
/// <param name="AuthorId">The author's user id.</param>
/// <param name="AuthorName">The author's display name.</param>
/// <param name="AuthorAvatar">The author's avatar CSS class.</param>
/// <param name="Body">The Markdown body.</param>
/// <param name="CreatedAt">When the comment was posted.</param>
/// <param name="UpdatedAt">When the comment was last edited; null if never edited.</param>
/// <param name="ReplyCount">Total replies to this comment; always 0 for a reply.</param>
/// <param name="CanEdit">Whether the calling user may edit this comment.</param>
/// <param name="CanDelete">Whether the calling user may delete this comment.</param>
public sealed record DiscussionDto(
    Guid      Id,
    Guid      SprintTaskId,
    Guid?     ParentId,
    Guid      AuthorId,
    string    AuthorName,
    string    AuthorAvatar,
    string    Body,
    DateTime  CreatedAt,
    DateTime? UpdatedAt,
    int       ReplyCount,
    bool      CanEdit,
    bool      CanDelete);
