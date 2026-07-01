using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Repositories.DTOs;
using MediatR;

namespace DASHBOARD.Application.Repositories.Queries.ListRepositories;

/// <summary>Returns repositories visible to the caller.</summary>
/// <param name="Search">Optional name/code filter.</param>
/// <param name="IncludeArchived">When <c>true</c> and caller is a global admin, archived repositories are included.</param>
/// <param name="MemberOnly">When <c>true</c>, restricts results to repositories the caller is explicitly a member of — even for global admins.</param>
public sealed record ListRepositoriesQuery(
    string? Search          = null,
    bool    IncludeArchived = false) : IRequest<IReadOnlyList<RepositoryDto>>;
