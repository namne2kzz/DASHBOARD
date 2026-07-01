using DASHBOARD.Application.Auth.DTOs;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Auth.Queries.GetCurrentUser;

/// <summary>Handles <see cref="GetCurrentUserQuery"/>: fetches the current user's profile from the database using JWT claims.</summary>
public sealed class GetCurrentUserQueryHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    /// <summary>Returns the authenticated user's full profile.</summary>
    /// <param name="query">The query (no parameters — user ID is read from JWT claims).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="UserDto"/> with the user's profile data.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the JWT contains no valid user ID.</exception>
    /// <exception cref="NotFoundException">Thrown when the user no longer exists in the database.</exception>
    public async Task<UserDto> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            throw new UnauthorizedAccessException("No authenticated user found in token.");

        var user = await db.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId.Value, ct)
            ?? throw new NotFoundException(nameof(User), currentUser.UserId.Value);

        return new UserDto(
            UserId:      user.Id,
            Email:       user.Email,
            Name:        user.Name,
            AvatarClass: user.AvatarClass,
            IsGlobalAdmin: user.IsGlobalAdmin);
    }
}
