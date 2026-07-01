using FluentValidation;

namespace DASHBOARD.Application.SmartBoard.Commands.UpdateColumn;

/// <summary>Validates <see cref="UpdateColumnCommand"/> inputs.</summary>
internal sealed class UpdateColumnCommandValidator : AbstractValidator<UpdateColumnCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public UpdateColumnCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.ColumnId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MappedState).IsInEnum();
        RuleFor(x => x.WipLimit).GreaterThanOrEqualTo(0)
            .WithMessage("WIP limit must be 0 (unlimited) or a positive number.");
        RuleFor(x => x.AgingLimitDays).GreaterThan(0)
            .WithMessage("Aging limit must be at least 1 day.");
    }
}
