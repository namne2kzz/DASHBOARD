using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.SprintTasks.Commands.SetSprintTaskMetadata;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.SprintTasks.Commands;

/// <summary>
/// Unit tests for <see cref="SetSprintTaskMetadataCommandHandler"/>.
/// </summary>
/// <remarks>
/// The command replaces a work item's whole metadata selection rather than adding to it, so the
/// handler diffs the request against what is stored: unlisted rows are removed, new ones inserted,
/// and ids that are not real catalog values for this repository are dropped silently.
/// </remarks>
public sealed class SetSprintTaskMetadataCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _taskId       = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding one work item.</summary>
    public SetSprintTaskMetadataCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<SprintTask>().Add(new SprintTask
        {
            RepositoryId = _repositoryId,
            Type         = SprintTaskType.Task,
            Title        = "Task",
        }.WithId(_taskId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private SetSprintTaskMetadataCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.EditWorkItem, _repositoryId).Object;

    private async Task<RepositoryMetadata> AddCatalogValueAsync(
        string value = "v1.0", bool isGlobal = false, Guid? repositoryId = null)
    {
        var entry = new RepositoryMetadata
        {
            RepositoryId = isGlobal ? null : repositoryId ?? _repositoryId,
            IsGlobal     = isGlobal,
            Key          = MetadataKey.FixedInVersion,
            Value        = value,
        };
        _db.Set<RepositoryMetadata>().Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    private async Task AddLinkAsync(Guid metadataId, Guid? taskId = null)
    {
        _db.Set<WorkItemMetadata>().Add(new WorkItemMetadata
        {
            SprintTaskId = taskId ?? _taskId,
            MetadataId   = metadataId,
        });
        await _db.SaveChangesAsync();
    }

    private Task<List<Guid>> LinkedIdsAsync(Guid? taskId = null) =>
        _db.Set<WorkItemMetadata>().AsNoTracking()
            .Where(w => w.SprintTaskId == (taskId ?? _taskId))
            .Select(w => w.MetadataId)
            .ToListAsync();

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutEditWorkItemPrivilege_FailsAndChangesNothing()
    {
        var value = await AddCatalogValueAsync();
        var handler = CreateHandler(
            RequestUserContextMock.ForUser().AsMember(_repositoryId).Object);

        var act = () => handler.Handle(
            new SetSprintTaskMetadataCommand(_repositoryId, _taskId, [value.Id]),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await LinkedIdsAsync()).Should().BeEmpty();
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheWorkItemDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new SetSprintTaskMetadataCommand(_repositoryId, Guid.NewGuid(), []),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheWorkItemBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreignTaskId = Guid.NewGuid();
        _db.Set<SprintTask>().Add(new SprintTask
        {
            RepositoryId = Guid.NewGuid(),
            Type         = SprintTaskType.Task,
            Title        = "Foreign",
        }.WithId(foreignTaskId));
        await _db.SaveChangesAsync();

        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new SetSprintTaskMetadataCommand(_repositoryId, foreignTaskId, []),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Assigning values ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AttachesTheRequestedCatalogValues()
    {
        var first  = await AddCatalogValueAsync("v1.0");
        var second = await AddCatalogValueAsync("v2.0");

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new SetSprintTaskMetadataCommand(_repositoryId, _taskId, [first.Id, second.Id]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LinkedIdsAsync()).Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact]
    public async Task Handle_AcceptsAGlobalCatalogValue()
    {
        var global = await AddCatalogValueAsync("shared", isGlobal: true);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new SetSprintTaskMetadataCommand(_repositoryId, _taskId, [global.Id]),
            CancellationToken.None);

        (await LinkedIdsAsync()).Should().ContainSingle().Which.Should().Be(global.Id);
    }

    [Fact]
    public async Task Handle_IgnoresDuplicateIdsInTheRequest()
    {
        var value = await AddCatalogValueAsync();

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new SetSprintTaskMetadataCommand(_repositoryId, _taskId, [value.Id, value.Id]),
            CancellationToken.None);

        (await LinkedIdsAsync()).Should().ContainSingle();
    }

    // ── Rejecting values that are not in this catalog ───────────────────────

    [Fact]
    public async Task Handle_SilentlyDropsUnknownIds()
    {
        var valid = await AddCatalogValueAsync();

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new SetSprintTaskMetadataCommand(_repositoryId, _taskId, [valid.Id, Guid.NewGuid()]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("an unknown id is filtered out rather than failing the save");
        (await LinkedIdsAsync()).Should().ContainSingle().Which.Should().Be(valid.Id);
    }

    [Fact]
    public async Task Handle_DropsValuesBelongingToAnotherRepositorysCatalog()
    {
        var foreign = await AddCatalogValueAsync("v1.0", repositoryId: Guid.NewGuid());

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new SetSprintTaskMetadataCommand(_repositoryId, _taskId, [foreign.Id]),
            CancellationToken.None);

        (await LinkedIdsAsync()).Should().BeEmpty(
            "a catalog value is scoped to the repository that owns it");
    }

    // ── Replace semantics ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RemovesValuesLeftOutOfTheRequest()
    {
        var kept    = await AddCatalogValueAsync("kept");
        var dropped = await AddCatalogValueAsync("dropped");
        await AddLinkAsync(kept.Id);
        await AddLinkAsync(dropped.Id);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new SetSprintTaskMetadataCommand(_repositoryId, _taskId, [kept.Id]),
            CancellationToken.None);

        (await LinkedIdsAsync()).Should().ContainSingle().Which.Should().Be(kept.Id,
            "the command sets the whole selection rather than adding to it");
    }

    [Fact]
    public async Task Handle_WithAnEmptyRequest_ClearsEverySelection()
    {
        var value = await AddCatalogValueAsync();
        await AddLinkAsync(value.Id);

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new SetSprintTaskMetadataCommand(_repositoryId, _taskId, []),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await LinkedIdsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DoesNotDuplicateAValueThatIsAlreadyAttached()
    {
        var value = await AddCatalogValueAsync();
        await AddLinkAsync(value.Id);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new SetSprintTaskMetadataCommand(_repositoryId, _taskId, [value.Id]),
            CancellationToken.None);

        (await LinkedIdsAsync()).Should().ContainSingle(
            "re-sending the same selection is idempotent");
    }

    [Fact]
    public async Task Handle_LeavesOtherWorkItemsSelectionsAlone()
    {
        var otherTaskId = Guid.NewGuid();
        _db.Set<SprintTask>().Add(new SprintTask
        {
            RepositoryId = _repositoryId,
            Type         = SprintTaskType.Task,
            Title        = "Other",
        }.WithId(otherTaskId));
        await _db.SaveChangesAsync();

        var value = await AddCatalogValueAsync();
        await AddLinkAsync(value.Id, otherTaskId);

        var handler = CreateHandler(AuthorizedUser());

        await handler.Handle(
            new SetSprintTaskMetadataCommand(_repositoryId, _taskId, []),
            CancellationToken.None);

        (await LinkedIdsAsync(otherTaskId)).Should().ContainSingle(
            "clearing one work item must not strip another");
    }
}
