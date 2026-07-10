using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Contracts;
using DASHBOARD.Application.Invitations.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Application.Invitations.Commands.CreateInvitation;

/// <summary>Handles <see cref="CreateInvitationCommand"/>.</summary>
internal sealed class CreateInvitationCommandHandler(
    IApplicationDbContext   db,
    IRequestUserContext     user,
    IInvitationTokenService tokenService,
    IPublishEndpoint        publisher,
    IAppSettings            settings) : IRequestHandler<CreateInvitationCommand, Result<InvitationDto>>
{

    /// <summary>
    /// Creates a new invite, revokes prior pending invites for the same email + repo, and publishes
    /// an <see cref="InvitationCreatedMessage"/> so the email consumer sends the SMTP message.
    /// </summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created invitation DTO, or a failure when the email is already registered in the system.</returns>
    public async Task<Result<InvitationDto>> Handle(CreateInvitationCommand request, CancellationToken ct)
    {
        if (!await user.CanAsync(request.RepositoryId, SystemFunction.InviteMembers, ct))
            return Result<InvitationDto>.Failure("You do not have permission to invite members to this repository.");

        if (!await db.Set<Repository>().AnyAsync(r => r.Id == request.RepositoryId && !r.IsArchived, ct))
            return Result<InvitationDto>.Failure("Repository not found or is archived.");

        var userExists = await db.Set<User>()
            .AsNoTracking()
            .AnyAsync(u => u.Email == request.Email, ct);

        if (userExists)
            return Result<InvitationDto>.Failure(
                "This email address is not eligible for an invitation.");

        // Revoke any prior pending invites for the same email + repo before issuing a fresh one.
        var priorInvites = await db.Set<Invitation>()
            .AsTracking()
            .Where(i => i.Email      == request.Email
                     && i.RepositoryId == request.RepositoryId
                     && i.Status     == InvitationStatus.Pending)
            .ToListAsync(ct);

        foreach (var prior in priorInvites)
            prior.Status = InvitationStatus.Revoked;

        var (rawToken, tokenHash) = tokenService.Generate();

        var invitation = new Invitation
        {
            Email           = request.Email,
            RepositoryId    = request.RepositoryId,
            InvitedByUserId = user.UserId,
            TokenHash       = tokenHash,
            ExpiresAt       = DateTime.UtcNow.Add(settings.InvitationTokenTtl),
            Status          = InvitationStatus.Pending,
        };

        db.Set<Invitation>().Add(invitation);

        var inviterName = await db.Set<User>()
            .AsNoTracking()
            .Where(u => u.Id == user.UserId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(ct) ?? "A team member";

        await db.SaveChangesAsync(ct);

        // Fragment (#) is never sent to servers and is excluded from Referer headers,
        // so the raw token is not exposed in server/CDN/proxy logs.
        var acceptLink = $"{settings.InvitationFrontendBaseUrl.TrimEnd('/')}/invite/accept#{rawToken}";
        await publisher.Publish(new InvitationCreatedMessage(
            request.Email,
            acceptLink,
            inviterName,
            (int)settings.InvitationTokenTtl.TotalMinutes), ct);

        return Result<InvitationDto>.Success(new InvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.RepositoryId,
            invitation.Status,
            invitation.ExpiresAt));
    }
}
