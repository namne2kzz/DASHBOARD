using FluentValidation;

namespace DASHBOARD.Application.Roles.Commands.UpdateRole;

/// <summary>Validates <see cref="UpdateRoleCommand"/> inputs.</summary>
internal sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.AllowedFunctions).NotNull();
    }
}
