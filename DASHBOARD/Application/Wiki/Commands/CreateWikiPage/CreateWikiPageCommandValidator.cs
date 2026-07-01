using FluentValidation;

namespace DASHBOARD.Application.Wiki.Commands.CreateWikiPage;

/// <summary>Validates <see cref="CreateWikiPageCommand"/> inputs.</summary>
internal sealed class CreateWikiPageCommandValidator : AbstractValidator<CreateWikiPageCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public CreateWikiPageCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).MaximumLength(100_000);
    }
}
