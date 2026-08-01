using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.History.DTOs;
using MediatR;

namespace DASHBOARD.Application.History.Queries.ListRepositoryHistory;

/// <summary>Lists the repository-wide audit log with optional user, date-range, and category filters.</summary>
/// <param name="RepositoryId">The repository to audit.</param>
/// <param name="AuthorId">Optional author filter.</param>
/// <param name="FromUtc">Optional inclusive lower bound on change time.</param>
/// <param name="ToUtc">Optional inclusive upper bound on change time.</param>
/// <param name="Category">Optional category — "created", "state", "assignment", or "update".</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page.</param>
public sealed record ListRepositoryHistoryQuery(
    Guid      RepositoryId,
    Guid?     AuthorId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string?   Category,
    int       Page,
    int       PageSize) : IRequest<PagedResult<AuditLogEntryDto>>;
