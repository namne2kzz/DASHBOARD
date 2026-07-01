using FluentValidation;

namespace DASHBOARD.Application.Discussions.Commands.AddDiscussion;

/// <summary>Validates <see cref="AddDiscussionCommand"/> inputs.</summary>
internal sealed class AddDiscussionCommandValidator : AbstractValidator<AddDiscussionCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public AddDiscussionCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.SprintTaskId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10_000);
    }
}
