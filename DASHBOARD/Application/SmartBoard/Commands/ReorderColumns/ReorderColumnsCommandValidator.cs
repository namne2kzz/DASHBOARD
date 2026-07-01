using FluentValidation;

namespace DASHBOARD.Application.SmartBoard.Commands.ReorderColumns;

/// <summary>Validates <see cref="ReorderColumnsCommand"/> inputs.</summary>
internal sealed class ReorderColumnsCommandValidator : AbstractValidator<ReorderColumnsCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public ReorderColumnsCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.OrderedColumnIds).NotEmpty()
            .WithMessage("At least one column ID must be provided.")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("Column IDs must not be empty GUIDs.");
    }
}
