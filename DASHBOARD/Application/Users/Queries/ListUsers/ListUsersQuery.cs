using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Users.DTOs;
using MediatR;

namespace DASHBOARD.Application.Users.Queries.ListUsers;

/// <summary>Returns a paged list of all users. Restricted to global admins.</summary>
/// <param name="Search">Optional full-text search against name or email.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page (1–100).</param>
public sealed record ListUsersQuery(
    string? Search,
    int     Page     = 1,
    int     PageSize = 20) : IRequest<PagedResult<SystemUserListItemDto>>;
