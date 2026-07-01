using FluentValidation;

namespace DASHBOARD.Application.Users.Commands.UpdateProfile;

/// <summary>Validates <see cref="UpdateProfileCommand"/> inputs before the handler runs.</summary>
internal sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.TargetUserId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.AvatarClass)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(@"^bg-[a-z]+-\d{3}$")
            .WithMessage("AvatarClass must be a valid Tailwind CSS color class (e.g. 'bg-sky-600').");
    }
}
