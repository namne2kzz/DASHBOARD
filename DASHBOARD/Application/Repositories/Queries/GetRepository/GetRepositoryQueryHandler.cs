using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Repositories.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Repositories.Queries.GetRepository;

/// <summary>Handles <see cref="GetRepositoryQuery"/>: checks membership then projects the repository row.</summary>
public sealed class GetRepositoryQueryHandler(
    IApplicationDbContext db,
    IRequestUserContext   user) : IRequestHandler<GetRepositoryQuery, RepositoryDto>
{
    /// <summary>Validates membership, loads the repository, and returns the DTO.</summary>
    /// <param name="query">The query containing the repository ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="RepositoryDto"/>.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller is not a member.</exception>
    /// <exception cref="NotFoundException">Thrown when the repository does not exist or is archived.</exception>
    public async Task<RepositoryDto> Handle(GetRepositoryQuery query, CancellationToken ct)
    {
        if (!await user.IsMemberOfAsync(query.RepositoryId, ct))
            throw new ForbiddenException("You are not a member of this repository.");

        var memberCount = await db.Set<RepositoryMember>()
            .AsNoTracking()
            .CountAsync(m => m.RepositoryId == query.RepositoryId, ct);

        var repo = await db.Set<Repository>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == query.RepositoryId && !r.IsArchived, ct)
            ?? throw new NotFoundException(nameof(Repository), query.RepositoryId);

        return new RepositoryDto(repo.Id, repo.Name, repo.Code, repo.Description, memberCount, repo.CreatedAt);
    }
}
