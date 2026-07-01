using FluentValidation;

namespace DASHBOARD.Application.Repositories.Commands.CreateRepository;

/// <summary>Validates <see cref="CreateRepositoryCommand"/> inputs.</summary>
internal sealed class CreateRepositoryCommandValidator : AbstractValidator<CreateRepositoryCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public CreateRepositoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(10)
            .Matches(@"^[A-Z0-9]+$")
            .WithMessage("Code must contain only uppercase letters and digits.");

        RuleFor(x => x.Description)
            .MaximumLength(1000);

        RuleFor(x => x.ScrumMasterId)
            .NotEmpty()
            .WithMessage("A Scrum Master must be assigned when creating a repository.");
    }
}
