using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Repositories.Commands.UpdateMetadata;
using DASHBOARD.Application.Repositories.Queries.GetRepository;
using DASHBOARD.Application.Repositories.Queries.ListMetadata;
using DASHBOARD.Application.SmartBoard.Queries.GetBoard;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Repositories;

/// <summary>
/// Unit tests for <see cref="GetRepositoryQueryHandler"/>, <see cref="ListMetadataQueryHandler"/>,
/// <see cref="UpdateMetadataCommandHandler"/> and <see cref="GetBoardQueryHandler"/>.
/// </summary>
public sealed class RepositoryReadAndMetadataUpdateTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated database holding one active repository.</summary>
    public RepositoryReadAndMetadataUpdateTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Repository>().Add(new Repository
        {
            Name        = "Dashboard",
            Code        = "DASH",
            Description = "Main project",
        }.WithId(_repositoryId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private GetRepositoryQueryHandler RepoHandler(IRequestUserContext user) => new(_db, user);

    private ListMetadataQueryHandler ListMetadataHandler(IRequestUserContext user) => new(_db, user);

    private UpdateMetadataCommandHandler UpdateMetadataHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private GetBoardQueryHandler BoardHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private IRequestUserContext MetadataManager() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageMetadata, _repositoryId).Object;

    private IRequestUserContext AdminUser() =>
        RequestUserContextMock.ForUser().AsGlobalAdmin().Object;

    private async Task AddMemberAsync(Guid? repositoryId = null)
    {
        _db.Set<RepositoryMember>().Add(new RepositoryMember
        {
            RepositoryId = repositoryId ?? _repositoryId,
            UserId       = Guid.NewGuid(),
            RoleId       = Guid.NewGuid(),
            DefaultRole  = "Developer",
        });
        await _db.SaveChangesAsync();
    }

    private async Task<RepositoryMetadata> AddMetadataAsync(
        string      value        = "v1.0",
        MetadataKey key          = MetadataKey.FixedInVersion,
        bool        isGlobal     = false,
        Guid?       repositoryId = null)
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

    private async Task<SmartBoardColumn> AddColumnAsync(
        string name, int order, Guid? repositoryId = null)
    {
        var column = new SmartBoardColumn
        {
            RepositoryId   = repositoryId ?? _repositoryId,
            Name           = name,
            MappedState    = SprintTaskState.Todo,
            WipLimit       = 5,
            WipMode        = WipMode.Soft,
            AgingLimitDays = 3,
            Order          = order,
        };
        _db.Set<SmartBoardColumn>().Add(column);
        await _db.SaveChangesAsync();
        return column;
    }

    // ── GetRepository ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetRepository_WhenTheUserIsNotAMember_Throws()
    {
        var act = () => RepoHandler(RequestUserContextMock.ForUser().Object).Handle(
            new GetRepositoryQuery(_repositoryId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetRepository_WhenItDoesNotExist_ThrowsNotFound()
    {
        var act = () => RepoHandler(RequestUserContextMock.ForUser().AsMember().Object).Handle(
            new GetRepositoryQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetRepository_WhenItIsArchived_ThrowsNotFound()
    {
        var archivedId = Guid.NewGuid();
        _db.Set<Repository>().Add(
            new Repository { Name = "Old", Code = "OLD", IsArchived = true }.WithId(archivedId));
        await _db.SaveChangesAsync();

        var act = () => RepoHandler(RequestUserContextMock.ForUser().AsMember().Object).Handle(
            new GetRepositoryQuery(archivedId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>(
            "an archived repository is no longer reachable through this query");
    }

    [Fact]
    public async Task GetRepository_ReturnsTheDetailsWithTheMemberCount()
    {
        await AddMemberAsync();
        await AddMemberAsync();

        var result = await RepoHandler(MemberUser()).Handle(
            new GetRepositoryQuery(_repositoryId), CancellationToken.None);

        result.Id.Should().Be(_repositoryId);
        result.Name.Should().Be("Dashboard");
        result.Code.Should().Be("DASH");
        result.Description.Should().Be("Main project");
        result.MemberCount.Should().Be(2);
    }

    [Fact]
    public async Task GetRepository_TheMemberCountIgnoresOtherRepositories()
    {
        await AddMemberAsync();
        await AddMemberAsync(repositoryId: Guid.NewGuid());

        var result = await RepoHandler(MemberUser()).Handle(
            new GetRepositoryQuery(_repositoryId), CancellationToken.None);

        result.MemberCount.Should().Be(1);
    }

    // ── ListMetadata ────────────────────────────────────────────────────────

    [Fact]
    public async Task ListMetadata_WhenTheUserIsNotAMember_Throws()
    {
        var act = () => ListMetadataHandler(RequestUserContextMock.ForUser().Object).Handle(
            new ListMetadataQuery(_repositoryId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ListMetadata_ReturnsThisRepositorysValuesPlusGlobalOnes()
    {
        await AddMetadataAsync("mine");
        await AddMetadataAsync("shared", isGlobal: true);
        await AddMetadataAsync("foreign", repositoryId: Guid.NewGuid());

        var result = await ListMetadataHandler(MemberUser()).Handle(
            new ListMetadataQuery(_repositoryId), CancellationToken.None);

        result.Select(m => m.Value).Should().BeEquivalentTo(["mine", "shared"],
            "a global value is available to every repository");
    }

    [Fact]
    public async Task ListMetadata_WithAKeyFilter_ReturnsOnlyThatCatalog()
    {
        await AddMetadataAsync("v1.0",   MetadataKey.FixedInVersion);
        await AddMetadataAsync("urgent", MetadataKey.Labels);

        var result = await ListMetadataHandler(MemberUser()).Handle(
            new ListMetadataQuery(_repositoryId, MetadataKey.Labels), CancellationToken.None);

        result.Should().ContainSingle().Which.Value.Should().Be("urgent");
    }

    [Fact]
    public async Task ListMetadata_OrdersByKeyThenValue()
    {
        await AddMetadataAsync("beta",  MetadataKey.Labels);
        await AddMetadataAsync("alpha", MetadataKey.Labels);
        await AddMetadataAsync("v1.0",  MetadataKey.FixedInVersion);

        var result = await ListMetadataHandler(MemberUser()).Handle(
            new ListMetadataQuery(_repositoryId), CancellationToken.None);

        // FixedInVersion precedes Labels in the enum, and values sort alphabetically inside a key.
        result.Select(m => m.Value).Should().ContainInOrder("v1.0", "alpha", "beta");
    }

    [Fact]
    public async Task ListMetadata_ProjectsTheKeyNameAndDisplayName()
    {
        await AddMetadataAsync("urgent", MetadataKey.Labels);

        var result = await ListMetadataHandler(MemberUser()).Handle(
            new ListMetadataQuery(_repositoryId), CancellationToken.None);

        var dto = result.Single();
        dto.Key.Should().Be(nameof(MetadataKey.Labels));
        dto.DisplayName.Should().NotBeNullOrWhiteSpace();
        dto.IsGlobal.Should().BeFalse();
    }

    // ── UpdateMetadata ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMetadata_WhenTheEntryDoesNotExist_ThrowsNotFound()
    {
        var act = () => UpdateMetadataHandler(MetadataManager()).Handle(
            new UpdateMetadataCommand(_repositoryId, Guid.NewGuid(), "v2.0"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateMetadata_WithoutManageMetadataPrivilege_FailsAndKeepsTheValue()
    {
        var entry = await AddMetadataAsync("v1.0");

        var result = await UpdateMetadataHandler(MemberUser()).Handle(
            new UpdateMetadataCommand(_repositoryId, entry.Id, "v2.0"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await _db.Set<RepositoryMetadata>().AsNoTracking().SingleAsync()).Value.Should().Be("v1.0");
    }

    [Fact]
    public async Task UpdateMetadata_AGlobalEntryRequiresAGlobalAdmin()
    {
        var entry = await AddMetadataAsync("shared", isGlobal: true);

        var result = await UpdateMetadataHandler(MetadataManager()).Handle(
            new UpdateMetadataCommand(_repositoryId, entry.Id, "renamed"), CancellationToken.None);

        result.IsFailure.Should().BeTrue("ManageMetadata on one repository is not global authority");
        result.Error.Should().Contain("global admins");
    }

    [Fact]
    public async Task UpdateMetadata_AGlobalEntryByAnAdmin_Succeeds()
    {
        var entry = await AddMetadataAsync("shared", isGlobal: true);

        var result = await UpdateMetadataHandler(AdminUser()).Handle(
            new UpdateMetadataCommand(_repositoryId, entry.Id, "renamed"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<RepositoryMetadata>().AsNoTracking().SingleAsync()).Value.Should().Be("renamed");
    }

    [Fact]
    public async Task UpdateMetadata_AuthorisesAgainstTheEntrysOwnRepository()
    {
        var foreignRepoId = Guid.NewGuid();
        var entry         = await AddMetadataAsync("v1.0", repositoryId: foreignRepoId);

        var result = await UpdateMetadataHandler(MetadataManager()).Handle(
            new UpdateMetadataCommand(_repositoryId, entry.Id, "v2.0"), CancellationToken.None);

        result.IsFailure.Should().BeTrue(
            "the repository id in the request must not override where the entry actually lives");
    }

    [Fact]
    public async Task UpdateMetadata_RenamesTheValueAndStampsTheTimestamp()
    {
        var entry = await AddMetadataAsync("v1.0");

        var result = await UpdateMetadataHandler(MetadataManager()).Handle(
            new UpdateMetadataCommand(_repositoryId, entry.Id, "v2.0"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _db.Set<RepositoryMetadata>().AsNoTracking().SingleAsync();
        updated.Value.Should().Be("v2.0");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateMetadata_WhenTheNewValueCollidesWithinTheSameKey_Fails()
    {
        var entry = await AddMetadataAsync("v1.0");
        await AddMetadataAsync("v2.0");

        var result = await UpdateMetadataHandler(MetadataManager()).Handle(
            new UpdateMetadataCommand(_repositoryId, entry.Id, "v2.0"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task UpdateMetadata_KeepingItsOwnValueIsNotACollision()
    {
        var entry = await AddMetadataAsync("v1.0");

        var result = await UpdateMetadataHandler(MetadataManager()).Handle(
            new UpdateMetadataCommand(_repositoryId, entry.Id, "v1.0"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("the check excludes the row being edited");
    }

    [Fact]
    public async Task UpdateMetadata_TheSameValueUnderAnotherKeyIsNotACollision()
    {
        var entry = await AddMetadataAsync("v1.0", MetadataKey.FixedInVersion);
        await AddMetadataAsync("urgent", MetadataKey.Labels);

        var result = await UpdateMetadataHandler(MetadataManager()).Handle(
            new UpdateMetadataCommand(_repositoryId, entry.Id, "urgent"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("catalogs are keyed independently");
    }

    // ── GetBoard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBoard_WhenTheUserIsNotAMember_Throws()
    {
        var act = () => BoardHandler(RequestUserContextMock.ForUser().Object).Handle(
            new GetBoardQuery(_repositoryId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetBoard_WithNoColumns_ReturnsEmpty()
    {
        var result = await BoardHandler(MemberUser()).Handle(
            new GetBoardQuery(_repositoryId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBoard_ReturnsColumnsInBoardOrder()
    {
        await AddColumnAsync("Third",  2);
        await AddColumnAsync("First",  0);
        await AddColumnAsync("Second", 1);

        var result = await BoardHandler(MemberUser()).Handle(
            new GetBoardQuery(_repositoryId), CancellationToken.None);

        result.Select(c => c.Name).Should().ContainInOrder("First", "Second", "Third");
    }

    [Fact]
    public async Task GetBoard_ReturnsOnlyColumnsOfThisRepository()
    {
        await AddColumnAsync("Mine",    0);
        await AddColumnAsync("Foreign", 0, repositoryId: Guid.NewGuid());

        var result = await BoardHandler(MemberUser()).Handle(
            new GetBoardQuery(_repositoryId), CancellationToken.None);

        result.Should().ContainSingle().Which.Name.Should().Be("Mine");
    }

    [Fact]
    public async Task GetBoard_ProjectsTheColumnConfiguration()
    {
        await AddColumnAsync("Todo", 0);

        var result = await BoardHandler(MemberUser()).Handle(
            new GetBoardQuery(_repositoryId), CancellationToken.None);

        var column = result.Single();
        column.RepositoryId.Should().Be(_repositoryId);
        column.MappedState.Should().Be(SprintTaskState.Todo);
        column.WipLimit.Should().Be(5);
        column.WipMode.Should().Be(WipMode.Soft);
        column.AgingLimitDays.Should().Be(3);
        column.Order.Should().Be(0);
    }
}
