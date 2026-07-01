using FluentValidation;

namespace DASHBOARD.Application.Users.Commands.CreateUser;

/// <summary>Validates <see cref="CreateUserCommand"/> inputs before the handler runs.</summary>
internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(320)
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);

        RuleFor(x => x.AvatarClass)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(@"^bg-[a-z]+-\d{3}$")
            .WithMessage("AvatarClass must be a valid Tailwind CSS color class (e.g. 'bg-sky-600').");
    }
}
