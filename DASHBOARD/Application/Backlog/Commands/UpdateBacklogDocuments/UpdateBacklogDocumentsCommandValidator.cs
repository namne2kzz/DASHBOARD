using FluentValidation;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogDocuments;

/// <summary>Validates <see cref="UpdateBacklogDocumentsCommand"/> inputs.</summary>
internal sealed class UpdateBacklogDocumentsCommandValidator : AbstractValidator<UpdateBacklogDocumentsCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public UpdateBacklogDocumentsCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Documents).NotNull();
        RuleForEach(x => x.Documents).NotEmpty().MaximumLength(500);
    }
}
