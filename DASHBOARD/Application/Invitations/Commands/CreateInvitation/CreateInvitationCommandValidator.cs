using FluentValidation;

namespace DASHBOARD.Application.Invitations.Commands.CreateInvitation;

/// <summary>Validates <see cref="CreateInvitationCommand"/>.</summary>
internal sealed class CreateInvitationCommandValidator : AbstractValidator<CreateInvitationCommand>
{
    /// <summary>Configures validation rules.</summary>
    public CreateInvitationCommandValidator()
    {
        RuleFor(x => x.RepositoryId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}
