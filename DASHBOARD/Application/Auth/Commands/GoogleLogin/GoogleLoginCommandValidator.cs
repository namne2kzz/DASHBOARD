using FluentValidation;

namespace DASHBOARD.Application.Auth.Commands.GoogleLogin;

/// <summary>FluentValidation rules for <see cref="GoogleLoginCommand"/>.</summary>
internal sealed class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
{
    /// <summary>Initializes validation rules for the Google login command.</summary>
    public GoogleLoginCommandValidator()
    {
        RuleFor(x => x.GoogleIdToken).NotEmpty().WithMessage("Google ID token is required.");
    }
}
