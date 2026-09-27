using DASHBOARD.Application.Users.Commands.UpdateProfile;

namespace DASHBOARD.UnitTests.Application.Users.Commands;

/// <summary>Unit tests for <see cref="UpdateProfileCommandValidator"/>.</summary>
public sealed class UpdateProfileCommandValidatorTests
{
    private readonly UpdateProfileCommandValidator _validator = new();

    private static UpdateProfileCommand Command(
        Guid?  targetUserId = null,
        string name         = "Valid Name",
        string avatarClass  = "bg-sky-600") =>
        new(targetUserId ?? Guid.NewGuid(), name, avatarClass);

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
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProfileCommand.TargetUserId));
    }

    // ── Name ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBlankName_Fails(string name)
    {
        var result = _validator.Validate(Command(name: name));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProfileCommand.Name));
    }

    [Fact]
    public void Validate_WithNameAtMaxLength_Passes()
    {
        _validator.Validate(Command(name: new string('a', 100))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNameOverMaxLength_Fails()
    {
        _validator.Validate(Command(name: new string('a', 101))).IsValid.Should().BeFalse();
    }

    // ── Avatar class ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("bg-sky-600")]
    [InlineData("bg-slate-100")]
    [InlineData("bg-emerald-900")]
    public void Validate_WithWellFormedAvatarClass_Passes(string avatarClass)
    {
        _validator.Validate(Command(avatarClass: avatarClass)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]                 // empty
    [InlineData("sky-600")]          // missing the bg- prefix
    [InlineData("bg-sky")]           // missing the shade
    [InlineData("bg-sky-60")]        // shade must be exactly three digits
    [InlineData("bg-sky-6000")]      // four digits
    [InlineData("bg-Sky-600")]       // uppercase is not allowed
    [InlineData("text-sky-600")]     // wrong utility prefix
    [InlineData("bg-sky-600; evil")] // trailing junk must not slip through
    public void Validate_WithMalformedAvatarClass_Fails(string avatarClass)
    {
        _validator.Validate(Command(avatarClass: avatarClass)).IsValid.Should().BeFalse();
    }
}
