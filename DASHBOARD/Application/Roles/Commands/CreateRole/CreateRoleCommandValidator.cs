using FluentValidation;

namespace DASHBOARD.Application.Roles.Commands.CreateRole;

/// <summary>Validates <see cref="CreateRoleCommand"/> inputs.</summary>
internal sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.AllowedFunctions).NotNull();
    }
}
