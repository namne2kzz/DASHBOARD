using DASHBOARD.Application.Common.Exceptions;
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
            throw new ForbiddenException("Only global admins may create user accounts.");

        var orgId = user.OrgId;

        // Email is unique per organization (multi-tenant).
        var emailTaken = await db.Set<User>()
            .IgnoreQueryFilters()
            .AnyAsync(u => u.OrgId == orgId && u.Email.ToLower() == command.Email.ToLower(), ct);

        if (emailTaken)
            throw new InvalidOperationException($"Email '{command.Email}' is already in use.");

        // Resolve the optional manager (must be a real user in the same org) so we can echo the name back.
        string? managerName = null;
        if (command.ManagerId is { } managerId)
        {
            managerName = await db.Set<User>().IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.Id == managerId && u.OrgId == orgId)
                .Select(u => u.Name)
                .FirstOrDefaultAsync(ct);
            if (managerName is null)
                throw new InvalidOperationException("The selected manager does not exist.");
        }

        var (hash, salt) = passwordService.HashPassword(command.Password);

        var newUser = new User
        {
            OrgId         = orgId,
            Name          = command.Name.Trim(),
            Email         = command.Email.Trim().ToLower(),
            PasswordHash  = hash,
            PasswordSalt  = salt,
            AvatarClass   = command.AvatarClass,
            IsGlobalAdmin = command.IsGlobalAdmin,
            AuthProvider  = AuthProvider.System,
            ManagerId     = command.ManagerId,
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
            AuthProvider:    newUser.AuthProvider,
            CreatedAt:       newUser.CreatedAt,
            LastLoginAt:     null,
            ManagerId:       command.ManagerId,
            ManagerName:     managerName,
            RepoMemberships: []);
    }
}
