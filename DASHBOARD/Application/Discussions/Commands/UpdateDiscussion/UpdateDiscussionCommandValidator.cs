using FluentValidation;

namespace DASHBOARD.Application.Discussions.Commands.UpdateDiscussion;

/// <summary>Validates <see cref="UpdateDiscussionCommand"/> inputs.</summary>
internal sealed class UpdateDiscussionCommandValidator : AbstractValidator<UpdateDiscussionCommand>
{
    /// <summary>Configures field-level validation rules, matching the limits applied when posting.</summary>
    public UpdateDiscussionCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.SprintTaskId).NotEmpty();
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10_000);
    }
}
