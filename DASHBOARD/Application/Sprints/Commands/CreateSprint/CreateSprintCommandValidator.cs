using FluentValidation;

namespace DASHBOARD.Application.Sprints.Commands.CreateSprint;

/// <summary>Validates <see cref="CreateSprintCommand"/> inputs.</summary>
internal sealed class CreateSprintCommandValidator : AbstractValidator<CreateSprintCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public CreateSprintCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("EndDate must be after StartDate.");
    }
}
