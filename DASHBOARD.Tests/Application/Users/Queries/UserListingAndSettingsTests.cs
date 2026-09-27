using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Users.Commands.UpsertUserSettings;
using DASHBOARD.Application.Users.Queries.GetUser;
using DASHBOARD.Application.Users.Queries.GetUserSettings;
using DASHBOARD.Application.Users.Queries.ListUsers;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Users.Queries;

/// <summary>
/// Unit tests for the user listing, profile lookup, and per-user preference handlers.
/// </summary>
public sealed class UserListingAndSettingsTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _orgId  = Guid.NewGuid();
    private readonly Guid                     _userId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public UserListingAndSettingsTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private ListUsersQueryHandler ListHandler(IRequestUserContext user) => new(_db, user);

    private GetUserQueryHandler GetHandler() => new(_db);

    private GetUserSettingsQueryHandler SettingsHandler(IRequestUserContext user) => new(_db, user);

    private UpsertUserSettingsCommandHandler UpsertHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AdminUser() =>
        RequestUserContextMock.ForUser(_userId, _orgId).AsGlobalAdmin().Object;

    private IRequestUserContext SelfUser() =>
        RequestUserContextMock.ForUser(_userId, _orgId).Object;

    private async Task<User> AddUserAsync(
        string name          = "Alice",
        Guid?  id            = null,
        Guid?  orgId         = null,
        bool   isDeleted     = false,
        bool   isGlobalAdmin = false,
        Guid?  managerId     = null)
    {
        var user = new User
        {
            OrgId         = orgId ?? _orgId,
            Name          = name,
            Email         = $"{name.ToLowerInvariant()}@test.local",
            PasswordHash  = "hash",
            PasswordSalt  = "salt",
            AvatarClass   = "bg-sky-600",
            IsGlobalAdmin = isGlobalAdmin,
            ManagerId     = managerId,
            IsDeleted     = isDeleted,
            DeletedAt     = isDeleted ? DateTime.UtcNow : null,
        };
        if (id.HasValue) user.WithId(id.Value);

        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── ListUsers: access control ───────────────────────────────────────────

    [Fact]
    public async Task List_WhenTheCallerIsNotAGlobalAdmin_Throws()
    {
        var handler = ListHandler(SelfUser());

        var act = () => handler.Handle(new ListUsersQuery(null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── ListUsers: multi-tenancy ────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsOnlyUsersOfTheCallersOrganization()
    {
        await AddUserAsync("Mine");
        await AddUserAsync("Foreign", orgId: Guid.NewGuid());

        var result = await ListHandler(AdminUser()).Handle(
            new ListUsersQuery(null), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Mine");
        result.TotalCount.Should().Be(1);
    }

    // ── ListUsers: deactivated accounts ─────────────────────────────────────

    [Fact]
    public async Task List_IncludesDeactivatedUsersAndFlagsThemInactive()
    {
        await AddUserAsync("Active");
        await AddUserAsync("Gone", isDeleted: true);

        var result = await ListHandler(AdminUser()).Handle(
            new ListUsersQuery(null), CancellationToken.None);

        result.Items.Should().HaveCount(2,
            "admin needs to see deactivated accounts in order to reactivate them");
        result.Items.Single(u => u.Name == "Gone").IsActive.Should().BeFalse();
        result.Items.Single(u => u.Name == "Active").IsActive.Should().BeTrue();
    }

    // ── ListUsers: search ───────────────────────────────────────────────────

    [Theory]
    // matches the name
    [InlineData("alice")]
    [InlineData("ALICE")]
    // matches the email
    [InlineData("alice@test")]
    public async Task List_SearchMatchesNameOrEmailIgnoringCase(string term)
    {
        await AddUserAsync("Alice");
        await AddUserAsync("Bob");

        var result = await ListHandler(AdminUser()).Handle(
            new ListUsersQuery(term), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Alice");
    }

    [Fact]
    public async Task List_WhenNothingMatches_ReturnsAnEmptyPage()
    {
        await AddUserAsync("Alice");

        var result = await ListHandler(AdminUser()).Handle(
            new ListUsersQuery("zzzz"), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ── ListUsers: paging ───────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsTheRequestedPageOrderedByName()
    {
        for (var i = 1; i <= 5; i++)
            await AddUserAsync($"User{i}");

        var page = await ListHandler(AdminUser()).Handle(
            new ListUsersQuery(null, Page: 2, PageSize: 2), CancellationToken.None);

        page.Items.Select(u => u.Name).Should().ContainInOrder("User3", "User4");
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task List_ReportsTheTotalAcrossEveryPage()
    {
        for (var i = 1; i <= 5; i++)
            await AddUserAsync($"User{i}");

        var page = await ListHandler(AdminUser()).Handle(
            new ListUsersQuery(null, Page: 1, PageSize: 2), CancellationToken.None);

        page.Items.Should().HaveCount(2);
        page.TotalCount.Should().Be(5, "the count is of matches, not of the page");
        page.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task List_TheTotalRespectsTheSearchFilter()
    {
        await AddUserAsync("Alice");
        await AddUserAsync("Alicia");
        await AddUserAsync("Bob");

        var page = await ListHandler(AdminUser()).Handle(
            new ListUsersQuery("ali", Page: 1, PageSize: 1), CancellationToken.None);

        page.TotalCount.Should().Be(2);
    }

    // ── ListUsers: projection ───────────────────────────────────────────────

    [Fact]
    public async Task List_ResolvesTheManagerName()
    {
        var manager = await AddUserAsync("Manager");
        await AddUserAsync("Report", managerId: manager.Id);

        var result = await ListHandler(AdminUser()).Handle(
            new ListUsersQuery("report"), CancellationToken.None);

        var report = result.Items.Single();
        report.ManagerId.Should().Be(manager.Id);
        report.ManagerName.Should().Be("Manager");
    }

    [Fact]
    public async Task List_ResolvesAManagerWhoIsNotOnTheSamePage()
    {
        // The manager sorts last by name, so a page-size of 1 leaves them off this page —
        // the handler still has to look their name up.
        var manager = await AddUserAsync("Zach");
        await AddUserAsync("Alice", managerId: manager.Id);

        var page = await ListHandler(AdminUser()).Handle(
            new ListUsersQuery(null, Page: 1, PageSize: 1), CancellationToken.None);

        page.Items.Single().ManagerName.Should().Be("Zach");
    }

    [Fact]
    public async Task List_LeavesTheManagerNameNullForRootUsers()
    {
        await AddUserAsync("Root");

        var result = await ListHandler(AdminUser()).Handle(
            new ListUsersQuery(null), CancellationToken.None);

        result.Items.Single().ManagerName.Should().BeNull();
    }

    [Fact]
    public async Task List_ProjectsTheIdentityFields()
    {
        await AddUserAsync("Alice", isGlobalAdmin: true);

        var result = await ListHandler(AdminUser()).Handle(
            new ListUsersQuery(null), CancellationToken.None);

        var item = result.Items.Single();
        item.Email.Should().Be("alice@test.local");
        item.AvatarClass.Should().Be("bg-sky-600");
        item.IsGlobalAdmin.Should().BeTrue();
        item.AuthProvider.Should().Be(AuthProvider.System);
    }

    // ── GetUser ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUser_WhenTheUserDoesNotExist_ThrowsNotFound()
    {
        var act = () => GetHandler().Handle(
            new GetUserQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetUser_ReturnsTheProfile()
    {
        var manager = await AddUserAsync("Manager");
        var alice   = await AddUserAsync("Alice", managerId: manager.Id);

        var result = await GetHandler().Handle(
            new GetUserQuery(alice.Id), CancellationToken.None);

        result.Id.Should().Be(alice.Id);
        result.Name.Should().Be("Alice");
        result.Email.Should().Be("alice@test.local");
        result.AvatarClass.Should().Be("bg-sky-600");
        result.ManagerId.Should().Be(manager.Id);
    }

    [Fact]
    public async Task GetUser_WhenTheUserIsDeactivated_ThrowsNotFound()
    {
        var gone = await AddUserAsync("Gone", isDeleted: true);

        var act = () => GetHandler().Handle(new GetUserQuery(gone.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>(
            "this lookup honours the soft-delete filter, unlike the admin listing");
    }

    // ── User settings: read ─────────────────────────────────────────────────

    [Fact]
    public async Task Settings_WithNothingStored_ReturnsAnEmptyMap()
    {
        var result = await SettingsHandler(SelfUser()).Handle(
            new GetUserSettingsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Settings_ReturnsOnlyTheSignedInUsersPreferences()
    {
        _db.Set<UserSetting>().Add(UserSetting.Create(_userId, "theme", "dark"));
        _db.Set<UserSetting>().Add(UserSetting.Create(Guid.NewGuid(), "theme", "light"));
        await _db.SaveChangesAsync();

        var result = await SettingsHandler(SelfUser()).Handle(
            new GetUserSettingsQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result["theme"].Should().Be("dark");
    }

    // ── User settings: upsert ───────────────────────────────────────────────

    [Fact]
    public async Task Upsert_WithAnEmptyPayload_Fails()
    {
        var result = await UpsertHandler(SelfUser()).Handle(
            new UpsertUserSettingsCommand([]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least one entry");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Upsert_WithABlankKey_Fails(string key)
    {
        var result = await UpsertHandler(SelfUser()).Handle(
            new UpsertUserSettingsCommand(new Dictionary<string, string?> { [key] = "value" }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must not be empty");
        (await _db.Set<UserSetting>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Upsert_InsertsNewPreferences()
    {
        var result = await UpsertHandler(SelfUser()).Handle(
            new UpsertUserSettingsCommand(new Dictionary<string, string?>
            {
                ["theme"]    = "dark",
                ["timezone"] = "Asia/Ho_Chi_Minh",
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var stored = await _db.Set<UserSetting>().AsNoTracking()
            .Where(s => s.UserId == _userId)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        stored.Should().HaveCount(2);
        stored["theme"].Should().Be("dark");
    }

    [Fact]
    public async Task Upsert_UpdatesAnExistingPreferenceInPlace()
    {
        _db.Set<UserSetting>().Add(UserSetting.Create(_userId, "theme", "light"));
        await _db.SaveChangesAsync();

        await UpsertHandler(SelfUser()).Handle(
            new UpsertUserSettingsCommand(new Dictionary<string, string?> { ["theme"] = "dark" }),
            CancellationToken.None);

        var rows = await _db.Set<UserSetting>().AsNoTracking()
            .Where(s => s.UserId == _userId).ToListAsync();

        rows.Should().ContainSingle("the key is updated rather than duplicated");
        rows[0].Value.Should().Be("dark");
    }

    [Fact]
    public async Task Upsert_LeavesUnmentionedPreferencesAlone()
    {
        _db.Set<UserSetting>().Add(UserSetting.Create(_userId, "theme", "dark"));
        _db.Set<UserSetting>().Add(UserSetting.Create(_userId, "timezone", "UTC"));
        await _db.SaveChangesAsync();

        await UpsertHandler(SelfUser()).Handle(
            new UpsertUserSettingsCommand(new Dictionary<string, string?> { ["theme"] = "light" }),
            CancellationToken.None);

        var stored = await _db.Set<UserSetting>().AsNoTracking()
            .Where(s => s.UserId == _userId)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        stored["timezone"].Should().Be("UTC",
            "this is a partial update, not a replacement of the whole preference set");
    }

    [Fact]
    public async Task Upsert_DoesNotTouchAnotherUsersPreferences()
    {
        var otherUserId = Guid.NewGuid();
        _db.Set<UserSetting>().Add(UserSetting.Create(otherUserId, "theme", "light"));
        await _db.SaveChangesAsync();

        await UpsertHandler(SelfUser()).Handle(
            new UpsertUserSettingsCommand(new Dictionary<string, string?> { ["theme"] = "dark" }),
            CancellationToken.None);

        var theirs = await _db.Set<UserSetting>().AsNoTracking()
            .SingleAsync(s => s.UserId == otherUserId);
        theirs.Value.Should().Be("light");
    }

    [Fact]
    public async Task Upsert_CanStoreANullValue()
    {
        var result = await UpsertHandler(SelfUser()).Handle(
            new UpsertUserSettingsCommand(new Dictionary<string, string?> { ["theme"] = null }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var stored = await _db.Set<UserSetting>().AsNoTracking().SingleAsync();
        stored.Value.Should().BeNull("clearing a preference is a legitimate edit");
    }
}
