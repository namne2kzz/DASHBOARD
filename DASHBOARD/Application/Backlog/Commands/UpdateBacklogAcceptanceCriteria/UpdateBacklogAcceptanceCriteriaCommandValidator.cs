using FluentValidation;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogAcceptanceCriteria;

/// <summary>Validates <see cref="UpdateBacklogAcceptanceCriteriaCommand"/> inputs.</summary>
internal sealed class UpdateBacklogAcceptanceCriteriaCommandValidator : AbstractValidator<UpdateBacklogAcceptanceCriteriaCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public UpdateBacklogAcceptanceCriteriaCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.AcceptanceCriteria).NotNull().MaximumLength(4000);
    }
}
