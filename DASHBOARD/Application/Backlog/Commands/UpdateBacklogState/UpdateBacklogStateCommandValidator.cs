using FluentValidation;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogState;

/// <summary>Validates <see cref="UpdateBacklogStateCommand"/> inputs.</summary>
internal sealed class UpdateBacklogStateCommandValidator : AbstractValidator<UpdateBacklogStateCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public UpdateBacklogStateCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.State).IsInEnum();
    }
}
