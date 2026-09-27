using DASHBOARD.Application.Sprints.Commands.CreateSprint;

namespace DASHBOARD.UnitTests.Application.Sprints.Commands;

/// <summary>Unit tests for <see cref="CreateSprintCommandValidator"/>.</summary>
public sealed class CreateSprintCommandValidatorTests
{
    private readonly CreateSprintCommandValidator _validator = new();

    private static CreateSprintCommand Command(
        Guid?     repositoryId = null,
        string    name         = "Sprint 1",
        DateOnly? start        = null,
        DateOnly? end          = null) =>
        new(repositoryId ?? Guid.NewGuid(),
            name,
            start ?? new DateOnly(2026, 5, 1),
            end   ?? new DateOnly(2026, 5, 14));

    [Fact]
    public void Validate_WithValidCommand_Passes()
    {
        var result = _validator.Validate(Command());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyRepositoryId_Fails()
    {
        var result = _validator.Validate(Command(repositoryId: Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSprintCommand.RepositoryId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBlankName_Fails(string name)
    {
        var result = _validator.Validate(Command(name: name));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSprintCommand.Name));
    }

    [Fact]
    public void Validate_WithNameAtMaxLength_Passes()
    {
        var result = _validator.Validate(Command(name: new string('a', 200)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNameOverMaxLength_Fails()
    {
        var result = _validator.Validate(Command(name: new string('a', 201)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSprintCommand.Name));
    }

    [Fact]
    public void Validate_WhenEndDatePrecedesStartDate_Fails()
    {
        var result = _validator.Validate(Command(
            start: new DateOnly(2026, 5, 14),
            end:   new DateOnly(2026, 5, 1)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "EndDate must be after StartDate.");
    }

    [Fact]
    public void Validate_WhenEndDateEqualsStartDate_Fails()
    {
        var sameDay = new DateOnly(2026, 5, 1);

        var result = _validator.Validate(Command(start: sameDay, end: sameDay));

        result.IsValid.Should().BeFalse("a sprint must span at least one full day");
        result.Errors.Should().Contain(e => e.ErrorMessage == "EndDate must be after StartDate.");
    }
}
