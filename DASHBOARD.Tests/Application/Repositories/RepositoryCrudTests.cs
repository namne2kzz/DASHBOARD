using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Repositories.Commands.ArchiveRepository;
using DASHBOARD.Application.Repositories.Commands.UpdateRepository;
using DASHBOARD.Application.Repositories.Queries.CheckRepositoryCode;
using DASHBOARD.Application.Repositories.Queries.ListRepositories;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Repositories;

/// <summary>
/// Unit tests for the repository update, archive, code-check and listing handlers.
/// </summary>
public sealed class RepositoryCrudTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _orgId  = Guid.NewGuid();
    private readonly Guid                     _userId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public RepositoryCrudTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private UpdateRepositoryCommandHandler UpdateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private ArchiveRepositoryCommandHandler ArchiveHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private CheckRepositoryCodeQueryHandler CheckCodeHandler() => new(_db);

    private ListRepositoriesQueryHandler ListHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext AdminUser() =>
        RequestUserContextMock.ForUser(_userId, _orgId).AsGlobalAdmin().Object;

    private IRequestUserContext PlainUser() =>
        RequestUserContextMock.ForUser(_userId, _orgId).Object;

    private async Task<Repository> AddRepositoryAsync(
        string name       = "Dashboard",
        string code       = "DASH",
        bool   isArchived = false,
        Guid?  orgId      = null)
    {
        var repo = new Repository
        {
            OrgId      = orgId ?? _orgId,
            Name       = name,
            Code       = code,
            IsArchived = isArchived,
        };
        _db.Set<Repository>().Add(repo);
        await _db.SaveChangesAsync();
        return repo;
    }

    private async Task AddMembershipAsync(Guid repositoryId)
    {
        _db.Set<RepositoryMember>().Add(new RepositoryMember
        {
            RepositoryId = repositoryId,
            UserId       = _userId,
            RoleId       = Guid.NewGuid(),
            DefaultRole  = "Developer",
        });
        await _db.SaveChangesAsync();
    }

    // ── Update ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WithoutEditRepositoryPrivilege_FailsAndKeepsTheDetails()
    {
        var repo    = await AddRepositoryAsync();
        var handler = UpdateHandler(PlainUser());

        var result = await handler.Handle(
            new UpdateRepositoryCommand(repo.Id, "Renamed", "New description"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");

        var unchanged = await _db.Set<Repository>().AsNoTracking().SingleAsync();
        unchanged.Name.Should().Be("Dashboard");
    }

    [Fact]
    public async Task Update_WhenTheRepositoryDoesNotExist_ThrowsNotFound()
    {
        var handler = UpdateHandler(
            RequestUserContextMock.ForUser(_userId, _orgId)
                .WithPrivilege(SystemFunction.EditRepository).Object);

        var act = () => handler.Handle(
            new UpdateRepositoryCommand(Guid.NewGuid(), "Renamed", "Description"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_AppliesTheNameAndDescription()
    {
        var repo    = await AddRepositoryAsync();
        var handler = UpdateHandler(
            RequestUserContextMock.ForUser(_userId, _orgId)
                .WithPrivilege(SystemFunction.EditRepository, repo.Id).Object);

        var result = await handler.Handle(
            new UpdateRepositoryCommand(repo.Id, "Renamed", "New description"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _db.Set<Repository>().AsNoTracking().SingleAsync();
        updated.Name.Should().Be("Renamed");
        updated.Description.Should().Be("New description");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_DoesNotChangeTheCode()
    {
        var repo    = await AddRepositoryAsync();
        var handler = UpdateHandler(
            RequestUserContextMock.ForUser(_userId, _orgId)
                .WithPrivilege(SystemFunction.EditRepository, repo.Id).Object);

        await handler.Handle(
            new UpdateRepositoryCommand(repo.Id, "Renamed", "Description"),
            CancellationToken.None);

        var updated = await _db.Set<Repository>().AsNoTracking().SingleAsync();
        updated.Code.Should().Be("DASH",
            "work-item keys already published use this code, so it is immutable here");
    }

    // ── Archive ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Archive_WhenTheCallerIsNotAGlobalAdmin_Fails()
    {
        var repo    = await AddRepositoryAsync();
        var handler = ArchiveHandler(PlainUser());

        var result = await handler.Handle(
            new ArchiveRepositoryCommand(repo.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("global admins");
        (await _db.Set<Repository>().AsNoTracking().SingleAsync()).IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task Archive_WhenTheRepositoryDoesNotExist_ThrowsNotFound()
    {
        var handler = ArchiveHandler(AdminUser());

        var act = () => handler.Handle(
            new ArchiveRepositoryCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Archive_MarksTheRepositoryArchived()
    {
        var repo    = await AddRepositoryAsync();
        var handler = ArchiveHandler(AdminUser());

        var result = await handler.Handle(
            new ArchiveRepositoryCommand(repo.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var archived = await _db.Set<Repository>().AsNoTracking().SingleAsync();
        archived.IsArchived.Should().BeTrue();
        archived.UpdatedAt.Should().NotBeNull();
    }

    // ── Code availability ───────────────────────────────────────────────────

    [Fact]
    public async Task CheckCode_WhenNothingUsesTheCode_ReportsAvailable()
    {
        var available = await CheckCodeHandler().Handle(
            new CheckRepositoryCodeQuery("FREE"), CancellationToken.None);

        available.Should().BeTrue();
    }

    [Theory]
    [InlineData("DASH")]
    [InlineData("dash")]
    [InlineData("DaSh")]
    public async Task CheckCode_ComparisonIgnoresCase(string code)
    {
        await AddRepositoryAsync(code: "DASH");

        var available = await CheckCodeHandler().Handle(
            new CheckRepositoryCodeQuery(code), CancellationToken.None);

        available.Should().BeFalse();
    }

    [Fact]
    public async Task CheckCode_TreatsACodeUsedByAnotherOrganizationAsTaken()
    {
        await AddRepositoryAsync(code: "DASH", orgId: Guid.NewGuid());

        var available = await CheckCodeHandler().Handle(
            new CheckRepositoryCodeQuery("DASH"), CancellationToken.None);

        // This check is deliberately global, unlike CreateRepository which enforces uniqueness
        // per organization — so the form warns earlier than the command would actually refuse.
        available.Should().BeFalse(
            "the availability check looks across every organization, not just the caller's");
    }

    // ── Listing ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ForAGlobalAdmin_ReturnsEveryRepositoryOfTheirOrganization()
    {
        await AddRepositoryAsync("First",  "ONE");
        await AddRepositoryAsync("Second", "TWO");

        var result = await ListHandler(AdminUser()).Handle(
            new ListRepositoriesQuery(), CancellationToken.None);

        result.Should().HaveCount(2, "an admin sees the whole organization without joining each repo");
    }

    [Fact]
    public async Task List_NeverCrossesTheOrganizationBoundary()
    {
        await AddRepositoryAsync("Mine",    "ONE");
        await AddRepositoryAsync("Foreign", "TWO", orgId: Guid.NewGuid());

        var result = await ListHandler(AdminUser()).Handle(
            new ListRepositoriesQuery(), CancellationToken.None);

        result.Should().ContainSingle().Which.Name.Should().Be("Mine");
    }

    [Fact]
    public async Task List_ForAPlainUser_ReturnsOnlyTheirOwnRepositories()
    {
        var mine = await AddRepositoryAsync("Mine", "ONE");
        await AddRepositoryAsync("Not mine", "TWO");
        await AddMembershipAsync(mine.Id);

        var result = await ListHandler(PlainUser()).Handle(
            new ListRepositoriesQuery(), CancellationToken.None);

        result.Should().ContainSingle().Which.Name.Should().Be("Mine");
    }

    [Fact]
    public async Task List_ExcludesArchivedRepositories()
    {
        await AddRepositoryAsync("Active",   "ONE");
        await AddRepositoryAsync("Archived", "TWO", isArchived: true);

        var result = await ListHandler(AdminUser()).Handle(
            new ListRepositoriesQuery(), CancellationToken.None);

        result.Should().ContainSingle().Which.Name.Should().Be("Active");
    }

    [Fact]
    public async Task List_OrdersByName()
    {
        await AddRepositoryAsync("Zulu",  "ZUL");
        await AddRepositoryAsync("Alpha", "ALP");

        var result = await ListHandler(AdminUser()).Handle(
            new ListRepositoriesQuery(), CancellationToken.None);

        result.Select(r => r.Name).Should().ContainInOrder("Alpha", "Zulu");
    }

    [Theory]
    // matches the name
    [InlineData("dash")]
    // matches the code
    [InlineData("DSH")]
    public async Task List_SearchMatchesNameOrCodeIgnoringCase(string term)
    {
        await AddRepositoryAsync("Dashboard", "DSH");
        await AddRepositoryAsync("Unrelated", "OTH");

        var result = await ListHandler(AdminUser()).Handle(
            new ListRepositoriesQuery(Search: term), CancellationToken.None);

        result.Should().ContainSingle().Which.Name.Should().Be("Dashboard");
    }

    [Fact]
    public async Task List_WhenNothingMatchesTheSearch_ReturnsEmpty()
    {
        await AddRepositoryAsync();

        var result = await ListHandler(AdminUser()).Handle(
            new ListRepositoriesQuery(Search: "zzzz"), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
