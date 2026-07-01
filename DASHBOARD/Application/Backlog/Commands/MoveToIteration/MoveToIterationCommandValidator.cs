using FluentValidation;

namespace DASHBOARD.Application.Backlog.Commands.MoveToIteration;

/// <summary>Validates <see cref="MoveToIterationCommand"/> inputs.</summary>
internal sealed class MoveToIterationCommandValidator : AbstractValidator<MoveToIterationCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public MoveToIterationCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
    }
}
