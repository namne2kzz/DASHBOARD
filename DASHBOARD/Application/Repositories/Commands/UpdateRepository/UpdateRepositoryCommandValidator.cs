using FluentValidation;

namespace DASHBOARD.Application.Repositories.Commands.UpdateRepository;

/// <summary>Validates <see cref="UpdateRepositoryCommand"/> inputs.</summary>
internal sealed class UpdateRepositoryCommandValidator : AbstractValidator<UpdateRepositoryCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public UpdateRepositoryCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
