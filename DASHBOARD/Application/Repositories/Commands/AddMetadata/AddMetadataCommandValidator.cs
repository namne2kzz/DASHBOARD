using FluentValidation;

namespace DASHBOARD.Application.Repositories.Commands.AddMetadata;

/// <summary>Validates <see cref="AddMetadataCommand"/> inputs.</summary>
internal sealed class AddMetadataCommandValidator : AbstractValidator<AddMetadataCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public AddMetadataCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Value).NotEmpty().MaximumLength(500);
    }
}
