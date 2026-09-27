using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Members.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Members.Commands.AddMember;

/// <summary>Handles <see cref="AddMemberCommand"/>: checks ManageMembers permission, ensures user and repo exist, prevents duplicate membership, then creates the member row.</summary>
public sealed class AddMemberCommandHandler(
    IApplicationDbContext db,
    IRequestUserContext   user,
    IUnitOfWork           uow) : IRequestHandler<AddMemberCommand, Result<MemberDto>>
{
    /// <summary>Validates permissions and constraints, creates the member row, and returns the member DTO.</summary>
    /// <param name="command">The add-member command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see cref="Result{T}.Success"/> with the newly created <see cref="MemberDto"/>; <see cref="Result{T}.Failure"/> on a business-rule violation.</returns>
    public async Task<Result<MemberDto>> Handle(AddMemberCommand command, CancellationToken ct)
    {
        if (!await user.CanAsync(command.RepositoryId, SystemFunction.ManageMembers, ct))
            throw new ForbiddenException("You do not have permission to manage members in this repository.");

        var targetUser = await db.Set<User>().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == command.UserId, ct);
        if (targetUser is null)
            return Result<MemberDto>.Failure("User not found.");

        var repoOrgId = await db.Set<Repository>().AsNoTracking()
            .Where(r => r.Id == command.RepositoryId && !r.IsArchived)
            .Select(r => (Guid?)r.OrgId)
            .FirstOrDefaultAsync(ct);
        if (repoOrgId is null)
            return Result<MemberDto>.Failure("Repository not found or is archived.");

        // Multi-tenant guard: a user can only be added to repositories within their own organization.
        if (targetUser.OrgId != repoOrgId.Value)
            return Result<MemberDto>.Failure("User does not belong to this organization.");

        var alreadyMember = await db.Set<RepositoryMember>()
            .AnyAsync(m => m.UserId == command.UserId && m.RepositoryId == command.RepositoryId, ct);
        if (alreadyMember)
            return Result<MemberDto>.Failure("User is already a member of this repository.");

        // Validate Role is either a global default role or a custom role within this repo.
        var role = await db.Set<Role>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == command.RoleId && (r.IsDefault || r.RepositoryId == command.RepositoryId), ct);
        if (role is null)
            return Result<MemberDto>.Failure("Role not found in this repository.");

        var member = new RepositoryMember
        {
            UserId       = command.UserId,
            RepositoryId = command.RepositoryId,
            DefaultRole  = command.DefaultRole,
            RoleId       = command.RoleId,
        };
        db.Set<RepositoryMember>().Add(member);
        await uow.CommitAsync(ct);

        return Result<MemberDto>.Success(new MemberDto(member.Id, targetUser.Id, targetUser.Name, targetUser.Email, targetUser.AvatarClass,
            member.DefaultRole, member.RoleId, role.Name, member.CreatedAt));
    }
}
