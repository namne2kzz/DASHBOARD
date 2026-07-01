using FluentValidation;

namespace DASHBOARD.Application.SprintTasks.Commands.ChangeSprintTaskState;

/// <summary>Validates <see cref="ChangeSprintTaskStateCommand"/> inputs.</summary>
public sealed class ChangeSprintTaskStateCommandValidator : AbstractValidator<ChangeSprintTaskStateCommand>
{
    /// <summary>Configures rules for repository ID, task ID, and state value.</summary>
    public ChangeSprintTaskStateCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.NewState).IsInEnum();
    }
}
