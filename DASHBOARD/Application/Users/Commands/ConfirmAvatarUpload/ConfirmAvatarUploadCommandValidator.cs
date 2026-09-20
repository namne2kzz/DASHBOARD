using FluentValidation;

namespace DASHBOARD.Application.Users.Commands.ConfirmAvatarUpload;

/// <summary>Validates <see cref="ConfirmAvatarUploadCommand"/> inputs before the handler runs.</summary>
internal sealed class ConfirmAvatarUploadCommandValidator : AbstractValidator<ConfirmAvatarUploadCommand>
{
    /// <summary>Configures field-level validation rules.</summary>
    public ConfirmAvatarUploadCommandValidator()
    {
        RuleFor(x => x.ObjectKey)
            .NotEmpty()
            .MaximumLength(500)
            .Matches(@"^avatars/[^/]+/\d+\.jpg$")
            .WithMessage("ObjectKey must match the format 'avatars/{userId}/{timestamp}.jpg'.");
    }
}
