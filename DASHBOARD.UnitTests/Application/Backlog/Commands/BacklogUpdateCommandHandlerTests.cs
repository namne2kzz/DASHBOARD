using DASHBOARD.Application.Backlog.Commands.UpdateBacklogAcceptanceCriteria;
using DASHBOARD.Application.Backlog.Commands.UpdateBacklogDocuments;
using DASHBOARD.Application.Backlog.Commands.UpdateBacklogItem;
using DASHBOARD.Application.Backlog.Commands.UpdateBacklogState;
using DASHBOARD.Application.Backlog.Commands.UpdateBacklogTitle;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Backlog.Commands;

/// <summary>
/// Unit tests for the five backlog update commands — the full-form edit plus the four
/// single-field variants the UI uses for inline editing.
/// </summary>
public sealed class BacklogUpdateCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public BacklogUpdateCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private UpdateBacklogItemCommandHandler ItemHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private UpdateBacklogStateCommandHandler StateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private UpdateBacklogTitleCommandHandler TitleHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private UpdateBacklogAcceptanceCriteriaCommandHandler CriteriaHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private UpdateBacklogDocumentsCommandHandler DocumentsHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageBacklog, _repositoryId).Object;

    private IRequestUserContext PlainMember() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private async Task<BacklogItem> AddItemAsync(
        string           title        = "Original",
        BacklogItemState state        = BacklogItemState.New,
        Guid?            repositoryId = null)
    {
        var item = new BacklogItem
        {
            RepositoryId       = repositoryId ?? _repositoryId,
            Type               = BacklogItemType.UserStory,
            State              = state,
            Title              = title,
            Rank               = 1000m,
            AcceptanceCriteria = "Original criteria",
            Documents          = ["original.md"],
        };
        _db.Set<BacklogItem>().Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private Task<BacklogItem> LoadAsync(Guid id) =>
        _db.Set<BacklogItem>().AsNoTracking().SingleAsync(b => b.Id == id);

    private UpdateBacklogItemCommand FullEdit(
        Guid             itemId,
        string           title       = "Renamed",
        BacklogItemState state       = BacklogItemState.Ready,
        Guid?            sprintId    = null,
        int?             storyPoints = 8,
        TshirtSize?      tshirtSize  = null,
        string           criteria    = "New criteria",
        params string[]  documents) =>
        new(_repositoryId, itemId, title, state, sprintId, storyPoints, tshirtSize,
            criteria, documents.Length > 0 ? documents : ["spec.md"]);

    // ── Permission: every variant ───────────────────────────────────────────

    [Fact]
    public async Task FullEdit_WithoutManageBacklogPrivilege_FailsAndChangesNothing()
    {
        var item = await AddItemAsync();

        var act = () => ItemHandler(PlainMember()).Handle(
            FullEdit(item.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await LoadAsync(item.Id)).Title.Should().Be("Original");
    }

    [Fact]
    public async Task StateEdit_WithoutManageBacklogPrivilege_Fails()
    {
        var item = await AddItemAsync();

        var act = () => StateHandler(PlainMember()).Handle(
            new UpdateBacklogStateCommand(_repositoryId, item.Id, BacklogItemState.Ready),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await LoadAsync(item.Id)).State.Should().Be(BacklogItemState.New);
    }

    [Fact]
    public async Task TitleEdit_WithoutManageBacklogPrivilege_Fails()
    {
        var item = await AddItemAsync();

        var act = () => TitleHandler(PlainMember()).Handle(
            new UpdateBacklogTitleCommand(_repositoryId, item.Id, "Hijacked"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await LoadAsync(item.Id)).Title.Should().Be("Original");
    }

    [Fact]
    public async Task CriteriaEdit_WithoutManageBacklogPrivilege_Fails()
    {
        var item = await AddItemAsync();

        var act = () => CriteriaHandler(PlainMember()).Handle(
            new UpdateBacklogAcceptanceCriteriaCommand(_repositoryId, item.Id, "Hijacked"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task DocumentsEdit_WithoutManageBacklogPrivilege_Fails()
    {
        var item = await AddItemAsync();

        var act = () => DocumentsHandler(PlainMember()).Handle(
            new UpdateBacklogDocumentsCommand(_repositoryId, item.Id, ["hijacked.md"]),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ── Not found: repository scoping ───────────────────────────────────────

    [Fact]
    public async Task FullEdit_WhenTheItemDoesNotExist_ThrowsNotFound()
    {
        var act = () => ItemHandler(AuthorizedUser()).Handle(
            FullEdit(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task FullEdit_WhenTheItemBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddItemAsync(repositoryId: Guid.NewGuid());

        var act = () => ItemHandler(AuthorizedUser()).Handle(
            FullEdit(foreign.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task TitleEdit_WhenTheItemBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await AddItemAsync(repositoryId: Guid.NewGuid());

        var act = () => TitleHandler(AuthorizedUser()).Handle(
            new UpdateBacklogTitleCommand(_repositoryId, foreign.Id, "Renamed"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Committed is never set directly ─────────────────────────────────────

    [Fact]
    public async Task FullEdit_RejectsSettingStateToCommitted()
    {
        var item = await AddItemAsync();

        var result = await ItemHandler(AuthorizedUser()).Handle(
            FullEdit(item.Id, state: BacklogItemState.Committed), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Promote to Sprint");
        (await LoadAsync(item.Id)).State.Should().Be(BacklogItemState.New,
            "the whole edit is rejected, not just the state field");
    }

    [Fact]
    public async Task StateEdit_RejectsSettingStateToCommitted()
    {
        var item = await AddItemAsync();

        var result = await StateHandler(AuthorizedUser()).Handle(
            new UpdateBacklogStateCommand(_repositoryId, item.Id, BacklogItemState.Committed),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Promote to Sprint");
    }

    [Theory]
    [InlineData(BacklogItemState.New)]
    [InlineData(BacklogItemState.Refining)]
    [InlineData(BacklogItemState.Ready)]
    public async Task StateEdit_AcceptsEveryRefinementState(BacklogItemState state)
    {
        var item = await AddItemAsync();

        var result = await StateHandler(AuthorizedUser()).Handle(
            new UpdateBacklogStateCommand(_repositoryId, item.Id, state), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(item.Id)).State.Should().Be(state);
    }

    // ── Full edit ───────────────────────────────────────────────────────────

    [Fact]
    public async Task FullEdit_AppliesEveryFieldAndStampsTheTimestamp()
    {
        var item     = await AddItemAsync();
        var sprintId = Guid.NewGuid();

        var result = await ItemHandler(AuthorizedUser()).Handle(
            FullEdit(item.Id, "Renamed", BacklogItemState.Ready, sprintId,
                storyPoints: 13, criteria: "New criteria", documents: ["a.md", "b.md"]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await LoadAsync(item.Id);
        updated.Title.Should().Be("Renamed");
        updated.State.Should().Be(BacklogItemState.Ready);
        updated.SprintId.Should().Be(sprintId);
        updated.StoryPoints.Should().Be(13);
        updated.AcceptanceCriteria.Should().Be("New criteria");
        updated.Documents.Should().BeEquivalentTo(["a.md", "b.md"]);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FullEdit_CanClearTheEstimateAndSprint()
    {
        var item = await AddItemAsync();

        await ItemHandler(AuthorizedUser()).Handle(
            FullEdit(item.Id, sprintId: null, storyPoints: null), CancellationToken.None);

        var updated = await LoadAsync(item.Id);
        updated.StoryPoints.Should().BeNull();
        updated.SprintId.Should().BeNull();
    }

    [Fact]
    public async Task FullEdit_CarriesTheTshirtSize()
    {
        var item = await AddItemAsync();

        await ItemHandler(AuthorizedUser()).Handle(
            FullEdit(item.Id, storyPoints: null, tshirtSize: TshirtSize.L), CancellationToken.None);

        (await LoadAsync(item.Id)).TshirtSize.Should().Be(TshirtSize.L);
    }

    [Fact]
    public async Task FullEdit_CopiesTheDocumentListByValue()
    {
        var item      = await AddItemAsync();
        var documents = new List<string> { "a.md" };

        await ItemHandler(AuthorizedUser()).Handle(
            new UpdateBacklogItemCommand(_repositoryId, item.Id, "Renamed",
                BacklogItemState.Ready, null, null, null, "criteria", documents),
            CancellationToken.None);

        documents.Add("mutated-after-the-call.md");

        (await LoadAsync(item.Id)).Documents.Should().ContainSingle(
            "the handler copies the list rather than holding the caller's instance");
    }

    // ── Single-field edits ──────────────────────────────────────────────────

    [Fact]
    public async Task TitleEdit_ChangesOnlyTheTitle()
    {
        var item = await AddItemAsync(state: BacklogItemState.Refining);

        var result = await TitleHandler(AuthorizedUser()).Handle(
            new UpdateBacklogTitleCommand(_repositoryId, item.Id, "Renamed"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await LoadAsync(item.Id);
        updated.Title.Should().Be("Renamed");
        updated.State.Should().Be(BacklogItemState.Refining, "an inline rename touches nothing else");
        updated.AcceptanceCriteria.Should().Be("Original criteria");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CriteriaEdit_ChangesOnlyTheAcceptanceCriteria()
    {
        var item = await AddItemAsync();

        var result = await CriteriaHandler(AuthorizedUser()).Handle(
            new UpdateBacklogAcceptanceCriteriaCommand(_repositoryId, item.Id, "Given…When…Then…"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await LoadAsync(item.Id);
        updated.AcceptanceCriteria.Should().Be("Given…When…Then…");
        updated.Title.Should().Be("Original");
    }

    [Fact]
    public async Task CriteriaEdit_AcceptsAnEmptyValue()
    {
        var item = await AddItemAsync();

        var result = await CriteriaHandler(AuthorizedUser()).Handle(
            new UpdateBacklogAcceptanceCriteriaCommand(_repositoryId, item.Id, ""),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("clearing the criteria is a legitimate edit");
        (await LoadAsync(item.Id)).AcceptanceCriteria.Should().BeEmpty();
    }

    [Fact]
    public async Task DocumentsEdit_ReplacesTheWholeList()
    {
        var item = await AddItemAsync();

        var result = await DocumentsHandler(AuthorizedUser()).Handle(
            new UpdateBacklogDocumentsCommand(_repositoryId, item.Id, ["a.md", "b.md"]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await LoadAsync(item.Id);
        updated.Documents.Should().BeEquivalentTo(["a.md", "b.md"],
            "the list is set, not appended to");
        updated.Title.Should().Be("Original");
    }

    [Fact]
    public async Task DocumentsEdit_WithAnEmptyListClearsTheAttachments()
    {
        var item = await AddItemAsync();

        var result = await DocumentsHandler(AuthorizedUser()).Handle(
            new UpdateBacklogDocumentsCommand(_repositoryId, item.Id, []),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LoadAsync(item.Id)).Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task SingleFieldEdits_DoNotAffectOtherItems()
    {
        var target    = await AddItemAsync("Target");
        var bystander = await AddItemAsync("Bystander");

        await TitleHandler(AuthorizedUser()).Handle(
            new UpdateBacklogTitleCommand(_repositoryId, target.Id, "Renamed"),
            CancellationToken.None);

        (await LoadAsync(bystander.Id)).Title.Should().Be("Bystander");
    }
}
