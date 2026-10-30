using FluentValidation;

namespace DASHBOARD.Application.Orgs.Commands.ProvisionOrganization;

/// <summary>Validates <see cref="ProvisionOrganizationCommand"/>.</summary>
/// <remarks>
/// Max lengths mirror the EF configuration so a bad payload fails here with a readable message
/// instead of at the database with a truncation error.
/// </remarks>
internal sealed class ProvisionOrganizationCommandValidator : AbstractValidator<ProvisionOrganizationCommand>
{
    /// <summary>Configures validation rules.</summary>
    public ProvisionOrganizationCommandValidator()
    {
        RuleFor(x => x.LicenceKey).NotEmpty().MaximumLength(500);
        RuleFor(x => x.OrgName).NotEmpty().MaximumLength(200);

        // The alias becomes a URL segment, so it must survive routing untouched.
        RuleFor(x => x.OrgAlias)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[a-z0-9][a-z0-9-]*$")
            .WithMessage("Org alias must be lowercase letters, digits or hyphens, and start with a letter or digit.");

        // No NotEmpty: the description is genuinely optional, and MaximumLength passes on null.
        RuleFor(x => x.OrgAbout).MaximumLength(2000);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.AdminName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(320);

        RuleFor(x => x.LicenceRepoCapacity).GreaterThan(0);

        RuleFor(x => x.LicenceExpireDate)
            .GreaterThanOrEqualTo(x => x.LicenceDueDate)
            .WithMessage("Licence expiry date cannot be earlier than its due date.");
    }
}
