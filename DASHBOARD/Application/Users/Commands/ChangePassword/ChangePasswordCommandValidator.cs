using FluentValidation;

namespace DASHBOARD.Application.Users.Commands.ChangePassword;

/// <summary>Validates <see cref="ChangePasswordCommand"/> inputs before the handler runs.</summary>
internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.TargetUserId).NotEmpty();

        RuleFor(x => x.OldPassword).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"\d").WithMessage("Password must contain at least one digit.")
            .Matches(@"[!@#$%^&*()_+\-=\[\]{}|;':"",./<>?]")
            .WithMessage("Password must contain at least one special character.");

        RuleFor(x => x).Must(x => x.OldPassword != x.NewPassword)
            .WithMessage("New password must be different from the current password.")
            .WithName("NewPassword");
    }
}
