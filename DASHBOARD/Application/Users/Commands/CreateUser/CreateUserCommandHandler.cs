using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Users.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Users.Commands.CreateUser;

/// <summary>Handles <see cref="CreateUserCommand"/>: enforces global-admin gate, validates unique email, hashes the password, and persists the new user.</summary>
public sealed class CreateUserCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IPasswordService      passwordService,
    IUnitOfWork           uow) : IRequestHandler<CreateUserCommand, SystemUserListItemDto>
{
    /// <summary>Creates a new user account after verifying the caller is a global admin and the email is not already taken.</summary>
    /// <param name="command">The create-user request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created user as a <see cref="SystemUserListItemDto"/>.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller is not a global admin.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the email is already in use.</exception>
    public async Task<SystemUserListItemDto> Handle(CreateUserCommand command, CancellationToken ct)
    {
        if (!await user.IsGlobalAdminAsync(ct))
            throw new UnauthorizedAccessException("Only global admins may create user accounts.");

        var emailTaken = await db.Set<User>()
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.ToLower() == command.Email.ToLower(), ct);

        if (emailTaken)
            throw new InvalidOperationException($"Email '{command.Email}' is already in use.");

        var (hash, salt) = passwordService.HashPassword(command.Password);

        var newUser = new User
        {
            Name          = command.Name.Trim(),
            Email         = command.Email.Trim().ToLower(),
            PasswordHash  = hash,
            PasswordSalt  = salt,
            AvatarClass   = command.AvatarClass,
            IsGlobalAdmin = command.IsGlobalAdmin,
            AuthProvider  = AuthProvider.System,
        };

        db.Set<User>().Add(newUser);
        await uow.CommitAsync(ct);

        return new SystemUserListItemDto(
            UserId:          newUser.Id,
            Name:            newUser.Name,
            Email:           newUser.Email,
            AvatarClass:     newUser.AvatarClass,
            IsGlobalAdmin:   newUser.IsGlobalAdmin,
            IsActive:        true,
            CreatedAt:       newUser.CreatedAt,
            LastLoginAt:     null,
            RepoMemberships: []);
    }
}
