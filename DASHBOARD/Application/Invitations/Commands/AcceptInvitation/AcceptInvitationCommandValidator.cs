using FluentValidation;

namespace DASHBOARD.Application.Invitations.Commands.AcceptInvitation;

/// <summary>Validates <see cref="AcceptInvitationCommand"/>.</summary>
internal sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    /// <summary>Configures validation rules.</summary>
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.RawToken).NotEmpty();
        RuleFor(x => x.GoogleIdToken).NotEmpty();
    }
}
