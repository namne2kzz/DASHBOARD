using FluentValidation;

namespace DASHBOARD.Application.Auth.Commands.Login;

/// <summary>FluentValidation rules for <see cref="LoginCommand"/>.</summary>
internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    /// <summary>Initializes validation rules for the login command.</summary>
    public LoginCommandValidator()
    {
        RuleFor(x => x.OrgAlias)
            .NotEmpty().WithMessage("Organization is required.")
            .MaximumLength(50);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(320);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.")
            .MaximumLength(200);
    }
}
