using FluentValidation;

namespace DASHBOARD.Application.Orgs.Queries.GetOrganizationByAlias;

/// <summary>Validates <see cref="GetOrganizationByAliasQuery"/>.</summary>
/// <remarks>
/// The same shape rule the provisioning validator enforces. Rejecting malformed input here means a
/// probe using random strings is turned away before it reaches the database.
/// </remarks>
internal sealed class GetOrganizationByAliasQueryValidator : AbstractValidator<GetOrganizationByAliasQuery>
{
    /// <summary>Configures validation rules.</summary>
    public GetOrganizationByAliasQueryValidator()
    {
        RuleFor(x => x.Alias)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[a-z0-9][a-z0-9-]*$")
            .WithMessage("Organization name must be lowercase letters, digits or hyphens.");
    }
}
