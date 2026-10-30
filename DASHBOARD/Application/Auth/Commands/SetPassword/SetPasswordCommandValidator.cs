using FluentValidation;

namespace DASHBOARD.Application.Auth.Commands.SetPassword;

/// <summary>Validates <see cref="SetPasswordCommand"/> inputs before the handler runs.</summary>
/// <remarks>
/// Strength rules mirror <c>ChangePasswordCommandValidator</c> — a password chosen here must be as
/// strong as one changed later.
/// </remarks>
internal sealed class SetPasswordCommandValidator : AbstractValidator<SetPasswordCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public SetPasswordCommandValidator()
    {
        RuleFor(x => x.RawToken).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"\d").WithMessage("Password must contain at least one digit.")
            .Matches(@"[!@#$%^&*()_+\-=\[\]{}|;':"",./<>?]")
            .WithMessage("Password must contain at least one special character.");
    }
}
