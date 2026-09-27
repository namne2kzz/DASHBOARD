using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Users.Commands.CreateUser;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DASHBOARD.Tests.Application.Users.Commands;

/// <summary>Unit tests for <see cref="CreateUserCommandHandler"/>.</summary>
public sealed class CreateUserCommandHandlerTests : IDisposable
{
    private const string Hash = "hashed";
    private const string Salt = "salted";

    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Mock<IPasswordService>   _passwords = new();
    private readonly Guid                     _orgId     = Guid.NewGuid();

    /// <summary>Sets up an isolated database and a password service returning fixed values.</summary>
    public CreateUserCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
        _passwords.Setup(p => p.HashPassword(It.IsAny<string>())).Returns((Hash, Salt));
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private CreateUserCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _passwords.Object, _database.Uow);

    private IRequestUserContext AdminUser(Guid? orgId = null) =>
        RequestUserContextMock.ForUser(orgId: orgId ?? _orgId).AsGlobalAdmin().Object;

    private static CreateUserCommand Command(
        string name          = "New User",
        string email         = "new@test.local",
        string password      = "Str0ng!Pass",
        bool   isGlobalAdmin = false,
        string avatarClass   = "bg-sky-600",
        Guid?  managerId     = null) =>
        new(name, email, password, isGlobalAdmin, avatarClass, managerId);

    private async Task<User> AddUserAsync(
        string name, string email, Guid? orgId = null, bool isDeleted = false)
    {
        var user = new User
        {
            OrgId        = orgId ?? _orgId,
            Name         = name,
            Email        = email,
            PasswordHash = "existing-hash",
            PasswordSalt = "existing-salt",
            IsDeleted    = isDeleted,
            DeletedAt    = isDeleted ? DateTime.UtcNow : null,
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRequesterIsNotGlobalAdmin_Throws()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser(orgId: _orgId).Object);

        var act = () => handler.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await _db.Set<User>().CountAsync()).Should().Be(0);
    }

    // ── Email uniqueness (per organization) ─────────────────────────────────

    [Fact]
    public async Task Handle_WhenEmailIsTakenInTheSameOrg_Throws()
    {
        await AddUserAsync("Existing", "taken@test.local");
        var handler = CreateHandler(AdminUser());

        var act = () => handler.Handle(Command(email: "taken@test.local"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in use*");
    }

    [Fact]
    public async Task Handle_EmailComparisonIsCaseInsensitive()
    {
        await AddUserAsync("Existing", "taken@test.local");
        var handler = CreateHandler(AdminUser());

        var act = () => handler.Handle(Command(email: "TAKEN@TEST.LOCAL"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_WhenEmailIsTakenByADeactivatedUser_StillThrows()
    {
        await AddUserAsync("Deactivated", "taken@test.local", isDeleted: true);
        var handler = CreateHandler(AdminUser());

        var act = () => handler.Handle(Command(email: "taken@test.local"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "IgnoreQueryFilters means a deactivated account still reserves its email");
    }

    [Fact]
    public async Task Handle_WhenEmailIsTakenInAnotherOrg_Succeeds()
    {
        await AddUserAsync("Foreign", "taken@test.local", orgId: Guid.NewGuid());
        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(Command(email: "taken@test.local"), CancellationToken.None);

        result.Email.Should().Be("taken@test.local",
            "emails are unique per organization, not globally");
    }

    // ── Organization scoping ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AssignsTheRequestersOrganizationToTheNewUser()
    {
        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(Command(), CancellationToken.None);

        var created = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == result.UserId);
        created.OrgId.Should().Be(_orgId, "a new user always lands in the creator's organization");
    }

    // ── Manager resolution ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenManagerDoesNotExist_Throws()
    {
        var handler = CreateHandler(AdminUser());

        var act = () => handler.Handle(Command(managerId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*manager does not exist*");
    }

    [Fact]
    public async Task Handle_WhenManagerBelongsToAnotherOrg_Throws()
    {
        var foreignManager = await AddUserAsync("Foreign", "mgr@other.local", orgId: Guid.NewGuid());
        var handler        = CreateHandler(AdminUser());

        var act = () => handler.Handle(Command(managerId: foreignManager.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "a manager from another tenant must not be reachable");
    }

    [Fact]
    public async Task Handle_WithManagerInTheSameOrg_EchoesManagerName()
    {
        var manager = await AddUserAsync("Manager", "mgr@test.local");
        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(Command(managerId: manager.Id), CancellationToken.None);

        result.ManagerId.Should().Be(manager.Id);
        result.ManagerName.Should().Be("Manager");
    }

    // ── Persistence and normalisation ───────────────────────────────────────

    [Fact]
    public async Task Handle_TrimsNameAndNormalisesEmailToLowercase()
    {
        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(
            Command(name: "  Padded Name  ", email: "  MiXeD@Test.Local  "), CancellationToken.None);

        result.Name.Should().Be("Padded Name");
        result.Email.Should().Be("mixed@test.local");

        var created = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == result.UserId);
        created.Name.Should().Be("Padded Name");
        created.Email.Should().Be("mixed@test.local");
    }

    [Fact]
    public async Task Handle_StoresTheHashedPasswordNeverThePlaintext()
    {
        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(Command(password: "Str0ng!Pass"), CancellationToken.None);

        _passwords.Verify(p => p.HashPassword("Str0ng!Pass"), Times.Once);

        var created = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == result.UserId);
        created.PasswordHash.Should().Be(Hash);
        created.PasswordSalt.Should().Be(Salt);
        created.PasswordHash.Should().NotBe("Str0ng!Pass");
    }

    [Fact]
    public async Task Handle_PersistsGlobalAdminFlagAndAvatar()
    {
        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(
            Command(isGlobalAdmin: true, avatarClass: "bg-rose-600"), CancellationToken.None);

        result.IsGlobalAdmin.Should().BeTrue();
        result.AvatarClass.Should().Be("bg-rose-600");

        var created = await _db.Set<User>().AsNoTracking().SingleAsync(u => u.Id == result.UserId);
        created.IsGlobalAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CreatesAnActiveSystemProviderAccount()
    {
        var handler = CreateHandler(AdminUser());

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsActive.Should().BeTrue();
        result.AuthProvider.Should().Be(AuthProvider.System);
        result.LastLoginAt.Should().BeNull();
        result.RepoMemberships.Should().BeEmpty();
    }
}
