using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Users.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Queries.GetUser;

/// <summary>Handles <see cref="GetUserQuery"/>: loads and projects the user profile.</summary>
public sealed class GetUserQueryHandler(IApplicationDbContext db) : IRequestHandler<GetUserQuery, UserProfileDto>
{
    /// <summary>Fetches the user and maps to <see cref="UserProfileDto"/>.</summary>
    /// <param name="query">The query containing the user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The user's public profile.</returns>
    /// <exception cref="NotFoundException">Thrown when no user with the given ID exists.</exception>
    public async Task<UserProfileDto> Handle(GetUserQuery query, CancellationToken ct)
    {
        var user = await db.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == query.UserId, ct)
            ?? throw new NotFoundException(nameof(User), query.UserId);

        return new UserProfileDto(user.Id, user.Name, user.Email, user.AvatarClass, user.IsGlobalAdmin, user.ManagerId);
    }
}
