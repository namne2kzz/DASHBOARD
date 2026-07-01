using FluentValidation;

namespace DASHBOARD.Application.Backlog.Commands.UpdateBacklogItem;

/// <summary>Validates <see cref="UpdateBacklogItemCommand"/> inputs.</summary>
internal sealed class UpdateBacklogItemCommandValidator : AbstractValidator<UpdateBacklogItemCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public UpdateBacklogItemCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.AcceptanceCriteria).MaximumLength(4000);
        RuleFor(x => x.StoryPoints).InclusiveBetween(1, 100).When(x => x.StoryPoints.HasValue);
        RuleFor(x => x).Must(x => !(x.StoryPoints.HasValue && x.TshirtSize.HasValue))
            .WithMessage("Specify either StoryPoints or TshirtSize, not both.")
            .WithName("Estimate");
    }
}
