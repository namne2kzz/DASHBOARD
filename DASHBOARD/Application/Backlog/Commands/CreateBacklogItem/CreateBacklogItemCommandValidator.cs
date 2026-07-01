using FluentValidation;
using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Application.Backlog.Commands.CreateBacklogItem;

/// <summary>Validates <see cref="CreateBacklogItemCommand"/> inputs.</summary>
internal sealed class CreateBacklogItemCommandValidator : AbstractValidator<CreateBacklogItemCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public CreateBacklogItemCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.AcceptanceCriteria).MaximumLength(4000);

        // Epics use T-shirt sizes; UserStories use story points.
        RuleFor(x => x.StoryPoints)
            .InclusiveBetween(1, 100)
            .When(x => x.StoryPoints.HasValue);

        // Cannot have both estimates simultaneously.
        RuleFor(x => x).Must(x => !(x.StoryPoints.HasValue && x.TshirtSize.HasValue))
            .WithMessage("Specify either StoryPoints or TshirtSize, not both.")
            .WithName("Estimate");
    }
}
