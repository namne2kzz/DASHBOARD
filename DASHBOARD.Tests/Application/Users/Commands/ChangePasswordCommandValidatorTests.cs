using DASHBOARD.Application.Users.Commands.ChangePassword;

namespace DASHBOARD.Tests.Application.Users.Commands;

/// <summary>Unit tests for <see cref="ChangePasswordCommandValidator"/> — the password policy.</summary>
public sealed class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    private static ChangePasswordCommand Command(
        Guid?  targetUserId = null,
        string oldPassword  = "Current1!",
        string newPassword  = "Brand-New1!") =>
        new(targetUserId ?? Guid.NewGuid(), oldPassword, newPassword);

    [Fact]
    public void Validate_WithValidCommand_Passes()
    {
        _validator.Validate(Command()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyTargetUserId_Fails()
    {
        var result = _validator.Validate(Command(targetUserId: Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ChangePasswordCommand.TargetUserId));
    }

    [Fact]
    public void Validate_WithEmptyOldPassword_Fails()
    {
        var result = _validator.Validate(Command(oldPassword: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ChangePasswordCommand.OldPassword));
    }

    // ── Password policy ─────────────────────────────────────────────────────

    [Theory]
    // 7 characters — one short of the minimum, though otherwise well-formed.
    [InlineData("Ab1!def", "MinimumLength")]
    [InlineData("",        "empty")]
    public void Validate_WithTooShortNewPassword_Fails(string newPassword, string because)
    {
        var result = _validator.Validate(Command(newPassword: newPassword));

        result.IsValid.Should().BeFalse(because);
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ChangePasswordCommand.NewPassword));
    }

    [Fact]
    public void Validate_WithNewPasswordAtMinimumLength_Passes()
    {
        _validator.Validate(Command(newPassword: "Ab1!defg")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNewPasswordAtMaximumLength_Passes()
    {
        // 128 characters, still satisfying every character-class rule.
        var atLimit = "Ab1!" + new string('x', 124);

        _validator.Validate(Command(newPassword: atLimit)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNewPasswordOverMaximumLength_Fails()
    {
        var overLimit = "Ab1!" + new string('x', 125);

        _validator.Validate(Command(newPassword: overLimit)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("abcdefg1!",  "Password must contain at least one uppercase letter.")]
    [InlineData("ABCDEFG1!",  "Password must contain at least one lowercase letter.")]
    [InlineData("Abcdefgh!",  "Password must contain at least one digit.")]
    [InlineData("Abcdefg12",  "Password must contain at least one special character.")]
    public void Validate_WithMissingCharacterClass_FailsWithThatMessage(
        string newPassword, string expectedMessage)
    {
        var result = _validator.Validate(Command(newPassword: newPassword));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == expectedMessage);
    }

    // ── Old vs new ──────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WhenNewPasswordEqualsOldPassword_Fails()
    {
        var same = "Current1!";

        var result = _validator.Validate(Command(oldPassword: same, newPassword: same));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "New password must be different from the current password.");
    }

    [Fact]
    public void Validate_WhenNewPasswordDiffersOnlyByCase_Passes()
    {
        // The rule is an exact-equality check, so flipping one letter's case counts as a different
        // password. The new value still has to satisfy every character-class rule on its own.
        var result = _validator.Validate(Command(oldPassword: "Current1!", newPassword: "CUrrent1!"));

        result.IsValid.Should().BeTrue();
    }
}
