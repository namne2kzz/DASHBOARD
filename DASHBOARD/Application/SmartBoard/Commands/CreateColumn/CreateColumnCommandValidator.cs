using FluentValidation;

namespace DASHBOARD.Application.SmartBoard.Commands.CreateColumn;

/// <summary>Validates <see cref="CreateColumnCommand"/> inputs.</summary>
internal sealed class CreateColumnCommandValidator : AbstractValidator<CreateColumnCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public CreateColumnCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MappedState).IsInEnum();
        RuleFor(x => x.WipLimit).GreaterThanOrEqualTo(0)
            .WithMessage("WIP limit must be 0 (unlimited) or a positive number.");
        RuleFor(x => x.AgingLimitDays).GreaterThan(0)
            .WithMessage("Aging limit must be at least 1 day.");
    }
}
