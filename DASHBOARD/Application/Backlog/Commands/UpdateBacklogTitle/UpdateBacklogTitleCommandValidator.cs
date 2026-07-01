using FluentValidation;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogTitle;

/// <summary>Validates <see cref="UpdateBacklogTitleCommand"/> inputs.</summary>
internal sealed class UpdateBacklogTitleCommandValidator : AbstractValidator<UpdateBacklogTitleCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public UpdateBacklogTitleCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
    }
}
