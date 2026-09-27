using DASHBOARD.Application.Auth.Commands.Login;

namespace DASHBOARD.Tests.Application.Auth.Commands;

/// <summary>Unit tests for <see cref="LoginCommandValidator"/>.</summary>
public sealed class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    private static LoginCommand Command(
        string orgAlias = "acme",
        string email    = "user@acme.local",
        string password = "Str0ng!Pass") =>
        new(orgAlias, email, password);

    [Fact]
    public void Validate_WithValidCommand_Passes()
    {
        _validator.Validate(Command()).IsValid.Should().BeTrue();
    }

    // ── Organization alias ──────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBlankOrgAlias_Fails(string alias)
    {
        var result = _validator.Validate(Command(orgAlias: alias));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Organization is required.");
    }

    [Fact]
    public void Validate_WithOrgAliasAtMaxLength_Passes()
    {
        _validator.Validate(Command(orgAlias: new string('a', 50))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithOrgAliasOverMaxLength_Fails()
    {
        _validator.Validate(Command(orgAlias: new string('a', 51))).IsValid.Should().BeFalse();
    }

    // ── Email ───────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithEmptyEmail_Fails()
    {
        var result = _validator.Validate(Command(email: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Email is required.");
    }

    [Theory]
    [InlineData("not-an-email")]          // no @ at all
    [InlineData("@no-local-part.local")]  // nothing before the @
    public void Validate_WithMalformedEmail_Fails(string email)
    {
        var result = _validator.Validate(Command(email: email));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "Email must be a valid email address.");
    }

    [Theory]
    [InlineData("missing@domain")]         // no dot in the host
    [InlineData("spaces in@email.local")]  // whitespace in the local part
    public void Validate_AcceptsAddressesAStricterParserWouldReject(string email)
    {
        // FluentValidation's EmailAddress() deliberately applies only a loose check — anything with
        // a non-empty local part and an @ passes. Real verification is the sign-in attempt itself,
        // which fails with the same generic message whether or not the address exists.
        _validator.Validate(Command(email: email)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmailOverMaxLength_Fails()
    {
        // 321 characters in total, one past the limit, while staying well-formed.
        var local = new string('a', 320 - "@acme.local".Length + 1);

        _validator.Validate(Command(email: $"{local}@acme.local")).IsValid.Should().BeFalse();
    }

    // ── Password ────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithEmptyPassword_Fails()
    {
        var result = _validator.Validate(Command(password: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Password is required.");
    }

    [Fact]
    public void Validate_WithPasswordBelowMinimumLength_Fails()
    {
        var result = _validator.Validate(Command(password: "12345"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "Password must be at least 6 characters.");
    }

    [Fact]
    public void Validate_WithPasswordAtMinimumLength_Passes()
    {
        _validator.Validate(Command(password: "123456")).IsValid.Should().BeTrue(
            "login only checks length — the strength policy belongs to ChangePassword");
    }

    [Fact]
    public void Validate_WithPasswordAtMaxLength_Passes()
    {
        _validator.Validate(Command(password: new string('a', 200))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithPasswordOverMaxLength_Fails()
    {
        _validator.Validate(Command(password: new string('a', 201))).IsValid.Should().BeFalse();
    }
}
