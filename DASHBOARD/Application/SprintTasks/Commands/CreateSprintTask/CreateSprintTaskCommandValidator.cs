using FluentValidation;

namespace DASHBOARD.Application.SprintTasks.Commands.CreateSprintTask;

/// <summary>Validates <see cref="CreateSprintTaskCommand"/> inputs.</summary>
internal sealed class CreateSprintTaskCommandValidator : AbstractValidator<CreateSprintTaskCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public CreateSprintTaskCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.SprintId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.StoryPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OriginalEstimate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StepsToReproduce).MaximumLength(5_000);
        RuleFor(x => x.Environment).MaximumLength(500);
        RuleFor(x => x.RootCause).MaximumLength(2_000);
        RuleFor(x => x.Solution).MaximumLength(2_000);
        RuleFor(x => x.Impaction).MaximumLength(1_000);
        RuleFor(x => x.UnitTest).MaximumLength(2_000);
        RuleFor(x => x.DesignReview).MaximumLength(2_000);
    }
}
