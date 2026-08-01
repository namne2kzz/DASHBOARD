namespace DASHBOARD.Application.History.DTOs;

/// <summary>A repository-wide audit-log row: a history entry enriched with its work item number and a derived category.</summary>
/// <param name="Id">The history entry identifier.</param>
/// <param name="SprintTaskId">The work item the change was made on.</param>
/// <param name="WorkItemNumber">Formatted work item number (e.g. "DASH-42").</param>
/// <param name="AuthorId">The user who made the change.</param>
/// <param name="AuthorName">The author's display name.</param>
/// <param name="AuthorAvatar">The author's avatar colour class.</param>
/// <param name="Message">The human-readable change description.</param>
/// <param name="Category">Derived coarse category — "created", "state", "assignment", or "update".</param>
/// <param name="CreatedAt">UTC timestamp of the change.</param>
public sealed record AuditLogEntryDto(
    Guid     Id,
    Guid     SprintTaskId,
    string   WorkItemNumber,
    Guid     AuthorId,
    string   AuthorName,
    string   AuthorAvatar,
    string   Message,
    string   Category,
    DateTime CreatedAt);
