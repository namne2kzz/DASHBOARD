using DASHBOARD.Application.Backlog.Commands.CreateBacklogItem;
using DASHBOARD.Domain.Enums;

namespace DASHBOARD.Tests.Application.Backlog.Commands;

/// <summary>Unit tests for <see cref="CreateBacklogItemCommandValidator"/>.</summary>
public sealed class CreateBacklogItemCommandValidatorTests
{
    private readonly CreateBacklogItemCommandValidator _validator = new();

    private static CreateBacklogItemCommand Command(
        Guid?           repositoryId = null,
        BacklogItemType type         = BacklogItemType.UserStory,
        string          title        = "As a user I want…",
        int?            storyPoints  = null,
        TshirtSize?     tshirtSize   = null,
        string          criteria     = "Given…When…Then…") =>
        new(repositoryId ?? Guid.NewGuid(), type, title, null, storyPoints, tshirtSize, criteria);

    [Fact]
    public void Validate_WithValidCommand_Passes()
    {
        _validator.Validate(Command()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyRepositoryId_Fails()
    {
        var result = _validator.Validate(Command(repositoryId: Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBacklogItemCommand.RepositoryId));
    }

    // ── Title ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithBlankTitle_Fails(string title)
    {
        var result = _validator.Validate(Command(title: title));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBacklogItemCommand.Title));
    }

    [Fact]
    public void Validate_WithTitleAtMaxLength_Passes()
    {
        _validator.Validate(Command(title: new string('a', 500))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithTitleOverMaxLength_Fails()
    {
        _validator.Validate(Command(title: new string('a', 501))).IsValid.Should().BeFalse();
    }

    // ── Acceptance criteria ─────────────────────────────────────────────────

    [Fact]
    public void Validate_WithCriteriaAtMaxLength_Passes()
    {
        _validator.Validate(Command(criteria: new string('a', 4000))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithCriteriaOverMaxLength_Fails()
    {
        _validator.Validate(Command(criteria: new string('a', 4001))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyCriteria_Passes()
    {
        _validator.Validate(Command(criteria: "")).IsValid.Should()
                  .BeTrue("acceptance criteria are optional at creation time");
    }

    // ── Story points range ──────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_WithStoryPointsInRange_Passes(int points)
    {
        _validator.Validate(Command(storyPoints: points)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_WithStoryPointsOutOfRange_Fails(int points)
    {
        var result = _validator.Validate(Command(storyPoints: points));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBacklogItemCommand.StoryPoints));
    }

    [Fact]
    public void Validate_WithNoStoryPoints_SkipsTheRangeRule()
    {
        _validator.Validate(Command(storyPoints: null)).IsValid.Should().BeTrue();
    }

    // ── Mutually exclusive estimates ────────────────────────────────────────

    [Fact]
    public void Validate_WithBothEstimates_Fails()
    {
        var result = _validator.Validate(Command(storyPoints: 5, tshirtSize: TshirtSize.M));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "Specify either StoryPoints or TshirtSize, not both.");
    }

    [Fact]
    public void Validate_WithOnlyTshirtSize_Passes()
    {
        _validator.Validate(Command(tshirtSize: TshirtSize.L)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNeitherEstimate_Passes()
    {
        _validator.Validate(Command()).IsValid.Should()
                  .BeTrue("an unestimated item is legitimate before refinement");
    }
}
