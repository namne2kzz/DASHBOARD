namespace DASHBOARD.Application.History.DTOs;

/// <summary>A single audit-trail entry for a sprint task (work item).</summary>
public sealed record HistoryDto(
    Guid     Id,
    Guid     SprintTaskId,
    Guid     AuthorId,
    string   AuthorName,
    string   AuthorAvatar,
    string   Message,
    DateTime CreatedAt);
