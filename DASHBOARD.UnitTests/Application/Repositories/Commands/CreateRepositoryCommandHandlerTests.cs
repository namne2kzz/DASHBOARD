using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Repositories.Commands.CreateRepository;
using DASHBOARD.Domain.Constants;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Repositories.Commands;

/// <summary>Unit tests for <see cref="CreateRepositoryCommandHandler"/>.</summary>
public sealed class CreateRepositoryCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _orgId = Guid.NewGuid();

    /// <summary>Sets up an isolated database with an organization allowing 10 repositories.</summary>
    public CreateRepositoryCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Organization>().Add(new Organization
        {
            Name                 = "Acme",
            Alias                = "acme",
            ContactEmail         = "admin@acme.local",
            LicenseKey           = "LICENSE",
            LicenseRepoCapacity  = 10,
        }.WithId(_orgId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private CreateRepositoryCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AdminUser() =>
        RequestUserContextMock.ForUser(orgId: _orgId).AsGlobalAdmin().Object;

    private async Task<User> AddUserAsync(Guid? orgId = null)
    {
        var user = new User
        {
            OrgId        = orgId ?? _orgId,
            Name         = "Scrum Master",
            Email        = $"{Guid.NewGuid():N}@acme.local",
            PasswordHash = "hash",
            PasswordSalt = "salt",
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task AddRepositoryAsync(string code, Guid? orgId = null)
    {
        _db.Set<Repository>().Add(new Repository
        {
            OrgId = orgId ?? _orgId,
            Name  = code,
            Code  = code,
        });
        await _db.SaveChangesAsync();
    }

    private static CreateRepositoryCommand Command(
        Guid scrumMasterId, string name = "New Project", string code = "NEW") =>
        new(name, code, "A description", scrumMasterId);

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheCallerIsNotAGlobalAdmin_Throws()
    {
        var scrumMaster = await AddUserAsync();
        var handler     = CreateHandler(RequestUserContextMock.ForUser(orgId: _orgId).Object);

        var act = () => handler.Handle(Command(scrumMaster.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<Repository>().CountAsync()).Should().Be(0);
    }

    // ── License capacity ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheLicenseCapacityIsReached_Throws()
    {
        var org = await _db.Set<Organization>().AsTracking().SingleAsync();
        org.LicenseRepoCapacity = 1;
        await _db.SaveChangesAsync();
        await AddRepositoryAsync("OLD");

        var scrumMaster = await AddUserAsync();
        var handler     = CreateHandler(AdminUser());

        var act = () => handler.Handle(Command(scrumMaster.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*license allows up to 1 repositories*");
    }

    [Fact]
    public async Task Handle_CountsCapacityPerOrganization()
    {
        var org = await _db.Set<Organization>().AsTracking().SingleAsync();
        org.LicenseRepoCapacity = 1;
        await _db.SaveChangesAsync();

        // Another tenant's repository must not consume this organization's allowance.
        await AddRepositoryAsync("FOREIGN", orgId: Guid.NewGuid());

        var scrumMaster = await AddUserAsync();
        var handler     = CreateHandler(AdminUser());

        var act = () => handler.Handle(Command(scrumMaster.Id), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ── Code uniqueness ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheCodeIsAlreadyUsedInTheOrganization_Throws()
    {
        await AddRepositoryAsync("NEW");
        var scrumMaster = await AddUserAsync();
        var handler     = CreateHandler(AdminUser());

        var act = () => handler.Handle(Command(scrumMaster.Id, code: "NEW"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Handle_TheCodeCollisionCheckIgnoresCase()
    {
        await AddRepositoryAsync("NEW");
        var scrumMaster = await AddUserAsync();
        var handler     = CreateHandler(AdminUser());

        var act = () => handler.Handle(Command(scrumMaster.Id, code: "new"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_TheSameCodeMayExistInAnotherOrganization()
    {
        await AddRepositoryAsync("NEW", orgId: Guid.NewGuid());
        var scrumMaster = await AddUserAsync();
        var handler     = CreateHandler(AdminUser());

        var result = await handler.Handle(Command(scrumMaster.Id, code: "NEW"), CancellationToken.None);

        result.Code.Should().Be("NEW", "codes are unique per organization, not globally");
    }

    [Fact]
    public async Task Handle_NormalisesTheCodeToUppercase()
    {
        var scrumMaster = await AddUserAsync();
        var handler     = CreateHandler(AdminUser());

        var result = await handler.Handle(Command(scrumMaster.Id, code: "abc"), CancellationToken.None);

        result.Code.Should().Be("ABC");
    }

    // ── Scrum Master validation ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheScrumMasterDoesNotExist_Throws()
    {
        var handler = CreateHandler(AdminUser());

        var act = () => handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

    [Fact]
    public async Task Handle_WhenTheScrumMasterBelongsToAnotherOrganization_Throws()
    {
        var foreignUser = await AddUserAsync(orgId: Guid.NewGuid());
        var handler     = CreateHandler(AdminUser());

        var act = () => handler.Handle(Command(foreignUser.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "a repository may only be run by someone inside the same tenant");
    }

    // ── Bootstrap ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CreatesTheRepositoryInTheCallersOrganization()
    {
        var scrumMaster = await AddUserAsync();
        var handler     = CreateHandler(AdminUser());

        var result = await handler.Handle(
            Command(scrumMaster.Id, "Dashboard", "DASH"), CancellationToken.None);

        var persisted = await _db.Set<Repository>().AsNoTracking().SingleAsync();
        persisted.Id.Should().Be(result.Id);
        persisted.OrgId.Should().Be(_orgId);
        persisted.Name.Should().Be("Dashboard");
        persisted.Code.Should().Be("DASH");
        result.MemberCount.Should().Be(1, "the Scrum Master is the founding member");
    }

    [Fact]
    public async Task Handle_SeedsTheRepositorysOwnDefaultRoles()
    {
        var scrumMaster = await AddUserAsync();
        var handler     = CreateHandler(AdminUser());

        var result = await handler.Handle(Command(scrumMaster.Id), CancellationToken.None);

        var roles = await _db.Set<Role>().AsNoTracking()
            .Where(r => r.RepositoryId == result.Id)
            .ToListAsync();

        roles.Should().HaveCount(DefaultRoleDefinitions.All.Count,
            "every repository gets its own copy of the standard roles");
        roles.Should().OnlyContain(r => r.IsDefault);
        roles.Select(r => r.Name).Should().Contain(DefaultRoleDefinitions.ScrumMaster);
    }

    [Fact]
    public async Task Handle_SeedsTheDisciplineMetadataCatalog()
    {
        var scrumMaster = await AddUserAsync();
        var handler     = CreateHandler(AdminUser());

        var result = await handler.Handle(Command(scrumMaster.Id), CancellationToken.None);

        var metadata = await _db.Set<RepositoryMetadata>().AsNoTracking()
            .Where(m => m.RepositoryId == result.Id && m.Key == MetadataKey.RepoRole)
            .ToListAsync();

        metadata.Should().HaveCount(DefaultRoleDefinitions.All.Count);
        metadata.Should().OnlyContain(m => !m.IsGlobal, "the catalog belongs to this repository");
    }

    [Fact]
    public async Task Handle_AddsTheScrumMasterAsTheFoundingMember()
    {
        var scrumMaster = await AddUserAsync();
        var handler     = CreateHandler(AdminUser());

        var result = await handler.Handle(Command(scrumMaster.Id), CancellationToken.None);

        var membership = await _db.Set<RepositoryMember>().AsNoTracking().SingleAsync();
        membership.UserId.Should().Be(scrumMaster.Id);
        membership.RepositoryId.Should().Be(result.Id);
        membership.DefaultRole.Should().Be(DefaultRoleDefinitions.ScrumMaster);

        var assignedRole = await _db.Set<Role>().AsNoTracking()
            .SingleAsync(r => r.Id == membership.RoleId);
        assignedRole.Name.Should().Be(DefaultRoleDefinitions.ScrumMaster,
            "the founding member must hold the role that can manage the repository");
    }

    [Fact]
    public async Task Handle_TwoRepositoriesGetIndependentRoleRows()
    {
        var scrumMaster = await AddUserAsync();
        var handler     = CreateHandler(AdminUser());

        var first  = await handler.Handle(Command(scrumMaster.Id, code: "ONE"), CancellationToken.None);
        var second = await handler.Handle(Command(scrumMaster.Id, code: "TWO"), CancellationToken.None);

        var firstRoleIds  = await _db.Set<Role>().AsNoTracking()
            .Where(r => r.RepositoryId == first.Id).Select(r => r.Id).ToListAsync();
        var secondRoleIds = await _db.Set<Role>().AsNoTracking()
            .Where(r => r.RepositoryId == second.Id).Select(r => r.Id).ToListAsync();

        firstRoleIds.Should().NotIntersectWith(secondRoleIds,
            "editing one repository's roles must not affect another's");
    }
}
