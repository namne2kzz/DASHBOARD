using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Common.Models;
using DASHBOARD.Application.Common.Models.Emails;
using DASHBOARD.Application.Orgs.DTOs;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DASHBOARD.Application.Orgs.Commands.ProvisionOrganization;

/// <summary>Handles <see cref="ProvisionOrganizationCommand"/>.</summary>
internal sealed class ProvisionOrganizationCommandHandler(
    IApplicationDbContext   db,
    IInvitationTokenService tokenService,
    IEmailService           email,
    ILogger<ProvisionOrganizationCommandHandler> logger,
    IAppSettings            settings) : IRequestHandler<ProvisionOrganizationCommand, Result<ProvisionOrganizationDto>>
{
    /// <summary>
    /// Creates the organisation, its first admin account and a one-time password-setup token in a
    /// single transaction.
    /// </summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Identifiers and links for the new tenant, or a failure when the alias is taken, the licence
    /// key is already bound, or the admin email is already used inside the new organisation.
    /// </returns>
    public async Task<Result<ProvisionOrganizationDto>> Handle(
        ProvisionOrganizationCommand request,
        CancellationToken ct)
    {
        var alias = request.OrgAlias.Trim().ToLowerInvariant();
        var adminEmail = request.AdminEmail.Trim().ToLowerInvariant();

        // ── Guards ──────────────────────────────────────────────────────────
        // Both of these are also unique indexes; checking first turns a 500 from a constraint
        // violation into a 409 that NKey can show to the operator.
        if (await db.Set<Organization>().AnyAsync(o => o.Alias == alias, ct))
        {
            return Result<ProvisionOrganizationDto>.Failure(
                $"Organisation alias '{alias}' is already taken.");
        }

        if (await db.Set<Organization>().AnyAsync(o => o.LicenseKey == request.LicenceKey, ct))
        {
            return Result<ProvisionOrganizationDto>.Failure(
                "This licence key is already bound to another organisation.");
        }

        // ── Organisation ────────────────────────────────────────────────────
        // Licence fields are a snapshot at activation time. NKey stays the source of truth; editing
        // a licence there does not reach back into what was provisioned here.
        var org = new Organization
        {
            Name                = request.OrgName.Trim(),
            Alias               = alias,
            ContactEmail        = request.ContactEmail.Trim(),
            About               = request.OrgAbout?.Trim() ?? string.Empty,
            LicenseKey          = request.LicenceKey,
            LicenseDueDate      = request.LicenceDueDate,
            LicenseExpireDate   = request.LicenceExpireDate,
            LicenseRepoCapacity = request.LicenceRepoCapacity,
        };
        db.Set<Organization>().Add(org);

        // ── First admin ─────────────────────────────────────────────────────
        // Email is unique per organisation, and this organisation is brand new, so no collision is
        // possible yet. PasswordHash/Salt stay empty: the account cannot be signed into until the
        // customer redeems the setup token below. Login must therefore reject empty hashes.
        var admin = new User
        {
            OrgId         = org.Id,
            Name          = request.AdminName.Trim(),
            Email         = adminEmail,
            PasswordHash  = string.Empty,
            PasswordSalt  = string.Empty,
            IsGlobalAdmin = true,
        };
        db.Set<User>().Add(admin);

        // ── Password-setup token ────────────────────────────────────────────
        var (rawToken, tokenHash) = tokenService.Generate();
        var expiresAt = DateTime.UtcNow.Add(settings.PasswordSetupTokenTtl);

        db.Set<PasswordSetupToken>().Add(new PasswordSetupToken
        {
            UserId    = admin.Id,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        });

        await db.SaveChangesAsync(ct);

        // The raw token goes in the URL fragment, not the query string, so it is never written to
        // server, proxy or CDN logs — same approach as the invitation accept link.
        var baseUrl        = settings.InvitationFrontendBaseUrl.TrimEnd('/');
        var loginUrl       = $"{baseUrl}/{alias}/login";
        var setPasswordUrl = $"{baseUrl}/set-password#{rawToken}";

        // ── Welcome email ───────────────────────────────────────────────────
        // Best-effort: the organisation exists and the caller needs its identifiers back even when
        // SMTP is down. The response still carries SetPasswordUrl, so an operator can deliver the
        // link by hand after reading this warning.
        try
        {
            await email.SendOrganizationWelcomeAsync(new OrganizationWelcomeEmailModel(
                ToEmail:        adminEmail,
                AdminName:      admin.Name,
                OrgName:        org.Name,
                LoginUrl:       loginUrl,
                SetPasswordUrl: setPasswordUrl,
                ExpiryHours:    (int)Math.Round(settings.PasswordSetupTokenTtl.TotalHours)), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Welcome email failed for organisation {Alias}; the setup link must be delivered manually.",
                alias);
        }

        return Result<ProvisionOrganizationDto>.Success(new ProvisionOrganizationDto(
            OrgId:               org.Id,
            OrgAlias:            alias,
            AdminUserId:         admin.Id,
            LoginUrl:            loginUrl,
            SetPasswordUrl:      setPasswordUrl,
            SetPasswordExpiresAt: expiresAt));
    }
}
