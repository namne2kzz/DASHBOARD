using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Repositories.Commands.AddMetadata;
using DASHBOARD.Application.Repositories.Commands.DeleteMetadata;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Repositories;

/// <summary>
/// Unit tests for the repository metadata catalog commands.
/// </summary>
/// <remarks>
/// Two permission regimes share these handlers: a global entry (<c>RepositoryId = null</c>) is
/// admin-only, while a repository-scoped entry needs <see cref="SystemFunction.ManageMetadata"/>
/// on that repository. The tests keep the two apart.
/// </remarks>
public sealed class MetadataCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _userId       = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public MetadataCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private AddMetadataCommandHandler AddHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private DeleteMetadataCommandHandler DeleteHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext MetadataManager() =>
        RequestUserContextMock.ForUser(_userId)
            .WithPrivilege(SystemFunction.ManageMetadata, _repositoryId).Object;

    private IRequestUserContext AdminUser() =>
        RequestUserContextMock.ForUser(_userId).AsGlobalAdmin().Object;

    private async Task<RepositoryMetadata> AddEntryAsync(
        string       value        = "v1.0",
        MetadataKey  key          = MetadataKey.FixedInVersion,
        bool         isGlobal     = false,
        Guid?        repositoryId = null)
    {
        var entry = new RepositoryMetadata
        {
            RepositoryId = isGlobal ? null : repositoryId ?? _repositoryId,
            IsGlobal     = isGlobal,
            Key          = key,
            Value        = value,
        };
        _db.Set<RepositoryMetadata>().Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    private AddMetadataCommand Command(
        string      value    = "v2.0",
        MetadataKey key      = MetadataKey.FixedInVersion,
        bool        isGlobal = false) =>
        new(_repositoryId, key, value, isGlobal);

    // ── Add: repository-scoped permission ───────────────────────────────────

    [Fact]
    public async Task Add_WithoutManageMetadataPrivilege_Throws()
    {
        var handler = AddHandler(
            RequestUserContextMock.ForUser(_userId).AsMember(_repositoryId).Object);

        var act = () => handler.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<RepositoryMetadata>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Add_WithManageMetadataPrivilege_CreatesARepositoryScopedEntry()
    {
        var handler = AddHandler(MetadataManager());

        var result = await handler.Handle(Command("v2.0"), CancellationToken.None);

        result.Value.Should().Be("v2.0");
        result.IsGlobal.Should().BeFalse();
        result.RepositoryId.Should().Be(_repositoryId);

        var persisted = await _db.Set<RepositoryMetadata>().AsNoTracking().SingleAsync();
        persisted.RepositoryId.Should().Be(_repositoryId);
    }

    // ── Add: global permission ──────────────────────────────────────────────

    [Fact]
    public async Task Add_AGlobalEntryRequiresAGlobalAdmin()
    {
        // ManageMetadata on the repository is not enough to write the shared catalog.
        var handler = AddHandler(MetadataManager());

        var act = () => handler.Handle(Command(isGlobal: true), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Add_AGlobalEntryIsStoredWithoutARepository()
    {
        var handler = AddHandler(AdminUser());

        var result = await handler.Handle(Command(isGlobal: true), CancellationToken.None);

        result.IsGlobal.Should().BeTrue();
        result.RepositoryId.Should().BeNull();

        var persisted = await _db.Set<RepositoryMetadata>().AsNoTracking().SingleAsync();
        persisted.RepositoryId.Should().BeNull("a global value belongs to no single repository");
    }

    // ── Add: duplicate rule ─────────────────────────────────────────────────

    [Fact]
    public async Task Add_WhenTheValueAlreadyExistsForTheKey_Throws()
    {
        await AddEntryAsync("v1.0");
        var handler = AddHandler(MetadataManager());

        var act = () => handler.Handle(Command("v1.0"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Add_TheSameValueUnderADifferentKeyIsAllowed()
    {
        await AddEntryAsync("v1.0", MetadataKey.FixedInVersion);
        var handler = AddHandler(MetadataManager());

        var act = () => handler.Handle(
            Command("v1.0", MetadataKey.ImplementedInBuild), CancellationToken.None);

        await act.Should().NotThrowAsync("catalogs are keyed independently");
    }

    [Fact]
    public async Task Add_TheSameValueInAnotherRepositoryIsAllowed()
    {
        await AddEntryAsync("v1.0", repositoryId: Guid.NewGuid());
        var handler = AddHandler(MetadataManager());

        var act = () => handler.Handle(Command("v1.0"), CancellationToken.None);

        await act.Should().NotThrowAsync("each repository keeps its own catalog");
    }

    [Fact]
    public async Task Add_AGlobalValueDoesNotBlockTheSameRepositoryValue()
    {
        await AddEntryAsync("v1.0", isGlobal: true);
        var handler = AddHandler(MetadataManager());

        var act = () => handler.Handle(Command("v1.0"), CancellationToken.None);

        await act.Should().NotThrowAsync(
            "the duplicate check compares within one scope, and global is its own scope");
    }

    // ── Delete ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WhenTheEntryDoesNotExist_ThrowsNotFound()
    {
        var handler = DeleteHandler(MetadataManager());

        var act = () => handler.Handle(
            new DeleteMetadataCommand(_repositoryId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_WithoutManageMetadataPrivilege_FailsAndKeepsTheEntry()
    {
        var entry   = await AddEntryAsync();
        var handler = DeleteHandler(
            RequestUserContextMock.ForUser(_userId).AsMember(_repositoryId).Object);

        var act = () => handler.Handle(
            new DeleteMetadataCommand(_repositoryId, entry.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<RepositoryMetadata>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Delete_ARepositoryEntryWithThePrivilege_Succeeds()
    {
        var entry   = await AddEntryAsync();
        var handler = DeleteHandler(MetadataManager());

        var result = await handler.Handle(
            new DeleteMetadataCommand(_repositoryId, entry.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<RepositoryMetadata>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_AGlobalEntryRequiresAGlobalAdmin()
    {
        var entry   = await AddEntryAsync(isGlobal: true);
        var handler = DeleteHandler(MetadataManager());

        var act = () => handler.Handle(
            new DeleteMetadataCommand(_repositoryId, entry.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<RepositoryMetadata>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Delete_AGlobalEntryByAnAdmin_Succeeds()
    {
        var entry   = await AddEntryAsync(isGlobal: true);
        var handler = DeleteHandler(AdminUser());

        var result = await handler.Handle(
            new DeleteMetadataCommand(_repositoryId, entry.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<RepositoryMetadata>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_AuthorisesAgainstTheEntrysOwnRepository()
    {
        // The command carries a repository id, but permission is checked against the repository
        // the entry actually belongs to — otherwise the id in the request would be a free pass.
        var foreignRepoId = Guid.NewGuid();
        var entry         = await AddEntryAsync(repositoryId: foreignRepoId);
        var handler       = DeleteHandler(MetadataManager());

        var act = () => handler.Handle(
            new DeleteMetadataCommand(_repositoryId, entry.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<RepositoryMetadata>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Delete_RemovesOnlyTheNamedEntry()
    {
        var target    = await AddEntryAsync("v1.0");
        var bystander = await AddEntryAsync("v2.0");
        var handler   = DeleteHandler(MetadataManager());

        await handler.Handle(
            new DeleteMetadataCommand(_repositoryId, target.Id), CancellationToken.None);

        (await _db.Set<RepositoryMetadata>().AnyAsync(m => m.Id == bystander.Id)).Should().BeTrue();
    }
}
