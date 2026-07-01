using FluentValidation;

namespace DASHBOARD.Application.Repositories.Commands.UpdateMetadata;

/// <summary>Validates <see cref="UpdateMetadataCommand"/> inputs.</summary>
internal sealed class UpdateMetadataCommandValidator : AbstractValidator<UpdateMetadataCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public UpdateMetadataCommandValidator()
    {
        RuleFor(x => x.MetadataId).NotEmpty();
        RuleFor(x => x.Value).NotEmpty().MaximumLength(500);
    }
}
