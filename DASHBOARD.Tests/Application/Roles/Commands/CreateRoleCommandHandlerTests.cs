using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Roles.Commands.CreateRole;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Roles.Commands;

/// <summary>
/// Unit tests for <see cref="CreateRoleCommandHandler"/>.
/// </summary>
/// <remarks>
/// BUG-005 (High) reported this handler returning a 500 with no UI message when a duplicate role
/// name was submitted, because it threw <c>InvalidOperationException</c> instead of returning a
/// result. The duplicate-name test asserts a <c>Result.Failure</c>, not an exception.
/// </remarks>
public sealed class CreateRoleCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public CreateRoleCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private CreateRoleCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageRoles, _repositoryId).Object;

    private async Task AddRoleAsync(string name, bool isDefault = false, Guid? repositoryId = null)
    {
        _db.Set<Role>().Add(new Role
        {
            Name             = name,
            IsDefault        = isDefault,
            RepositoryId     = isDefault ? null : repositoryId ?? _repositoryId,
            AllowedFunctions = [SystemFunction.ViewRepository],
        });
        await _db.SaveChangesAsync();
    }

    private CreateRoleCommand Command(string name = "QA Lead", string description = "Quality owner") =>
        new(_repositoryId, name, description, [SystemFunction.ViewRepository, SystemFunction.EditWorkItem]);

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageRolesPrivilege_FailsWithoutThrowing()
    {
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await _db.Set<Role>().CountAsync()).Should().Be(0);
    }

    // ── Name uniqueness ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNameIsAlreadyTaken_FailsWithoutThrowing()
    {
        await AddRoleAsync("QA Lead");
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command("QA Lead"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
        (await _db.Set<Role>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheNameIsTakenInAnotherRepository_Succeeds()
    {
        await AddRoleAsync("QA Lead", repositoryId: Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command("QA Lead"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("role names are unique per repository");
    }

    [Fact]
    public async Task Handle_WhenAGlobalDefaultRoleSharesTheName_Succeeds()
    {
        await AddRoleAsync("Developer", isDefault: true);
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command("Developer"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            "the uniqueness check only looks at custom roles in this repository");
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CreatesACustomRoleScopedToTheRepository()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("QA Lead");
        result.Value.Description.Should().Be("Quality owner");
        result.Value.IsDefault.Should().BeFalse();
        result.Value.RepositoryId.Should().Be(_repositoryId);
        result.Value.MemberCount.Should().Be(0, "a brand-new role has nobody assigned");

        var persisted = await _db.Set<Role>().AsNoTracking().SingleAsync();
        persisted.RepositoryId.Should().Be(_repositoryId);
        persisted.IsDefault.Should().BeFalse();
        persisted.AllowedFunctions.Should()
                 .BeEquivalentTo([SystemFunction.ViewRepository, SystemFunction.EditWorkItem]);
    }

    [Fact]
    public async Task Handle_AcceptsAnEmptyPermissionSet()
    {
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new CreateRoleCommand(_repositoryId, "Observer", "No permissions yet", []),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Permissions.Should().BeEmpty();
    }
}
