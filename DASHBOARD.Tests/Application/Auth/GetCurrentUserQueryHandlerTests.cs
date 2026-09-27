using DASHBOARD.Application.Auth.Queries.GetCurrentUser;
using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Tests.Common;
using Moq;

namespace DASHBOARD.Tests.Application.Auth;

/// <summary>Unit tests for <see cref="GetCurrentUserQueryHandler"/> — the "who am I" endpoint.</summary>
public sealed class GetCurrentUserQueryHandlerTests : IDisposable
{
    private readonly TestDatabase              _database;
    private readonly TestApplicationDbContext  _db;
    private readonly Mock<ICurrentUserService> _currentUser = new();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public GetCurrentUserQueryHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private GetCurrentUserQueryHandler CreateHandler() => new(_db, _currentUser.Object);

    private async Task<User> AddUserAsync(bool isGlobalAdmin = false, bool isDeleted = false)
    {
        var user = new User
        {
            Name          = "Test User",
            Email         = "user@acme.local",
            PasswordHash  = "hash",
            PasswordSalt  = "salt",
            AvatarClass   = "bg-emerald-600",
            IsGlobalAdmin = isGlobalAdmin,
            IsDeleted     = isDeleted,
            DeletedAt     = isDeleted ? DateTime.UtcNow : null,
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── Rejection paths ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheTokenCarriesNoUserId_Throws()
    {
        _currentUser.SetupGet(c => c.UserId).Returns((Guid?)null);

        var act = () => CreateHandler().Handle(new GetCurrentUserQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WhenTheAccountNoLongerExists_ThrowsNotFound()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());

        var act = () => CreateHandler().Handle(new GetCurrentUserQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenTheAccountWasDeactivated_ThrowsNotFound()
    {
        var user = await AddUserAsync(isDeleted: true);
        _currentUser.SetupGet(c => c.UserId).Returns(user.Id);

        var act = () => CreateHandler().Handle(new GetCurrentUserQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>(
            "a still-valid token must stop resolving once the account is deactivated");
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsTheProfileOfTheTokenHolder()
    {
        var user = await AddUserAsync(isGlobalAdmin: true);
        _currentUser.SetupGet(c => c.UserId).Returns(user.Id);

        var result = await CreateHandler().Handle(new GetCurrentUserQuery(), CancellationToken.None);

        result.UserId.Should().Be(user.Id);
        result.Email.Should().Be("user@acme.local");
        result.Name.Should().Be("Test User");
        result.AvatarClass.Should().Be("bg-emerald-600");
        result.IsGlobalAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ResolvesTheTokenHolderNotSomeOtherAccount()
    {
        await AddUserAsync();
        var target = await AddUserAsync();
        _currentUser.SetupGet(c => c.UserId).Returns(target.Id);

        var result = await CreateHandler().Handle(new GetCurrentUserQuery(), CancellationToken.None);

        result.UserId.Should().Be(target.Id);
    }
}
