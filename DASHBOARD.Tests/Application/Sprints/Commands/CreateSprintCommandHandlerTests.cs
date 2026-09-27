using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Sprints.Commands.CreateSprint;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;

using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DASHBOARD.Tests.Application.Sprints.Commands;

/// <summary>Unit tests for <see cref="CreateSprintCommandHandler"/>.</summary>
public sealed class CreateSprintCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Mock<IHubChannelService> _hub  = new();
    private readonly Mock<IAppSettings>       _settings = new();
    private readonly Guid                   _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database and default collaborator mocks.</summary>
    public CreateSprintCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
        _settings.SetupGet(x => x.HubChatBaseUrl).Returns(string.Empty);
        _settings.SetupGet(x => x.HubFrontendBaseUrl).Returns("https://hub.test");
    }

    /// <summary>Disposes the in-memory database context.</summary>
    public void Dispose() => _database.Dispose();

    private CreateSprintCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow, _hub.Object, _settings.Object,
            NullLogger<CreateSprintCommandHandler>.Instance);

    private CreateSprintCommand ValidCommand(
        DateOnly? start = null, DateOnly? end = null, bool createHubChannel = false) =>
        new(_repositoryId,
            "Sprint 1",
            start ?? new DateOnly(2026, 5, 1),
            end   ?? new DateOnly(2026, 5, 14),
            createHubChannel);

    private async Task SeedSprintAsync(DateOnly start, DateOnly end, Guid? repositoryId = null)
    {
        _db.Set<Sprint>().Add(new Sprint
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Name         = "Existing",
            StartDate    = start,
            EndDate      = end,
        });
        await _db.SaveChangesAsync();
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageSprintPrivilege_FailsAndCreatesNothing()
    {
        var user    = RequestUserContextMock.ForUser().Object;
        var handler = CreateHandler(user);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await _db.Set<Sprint>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_AsGlobalAdmin_Succeeds()
    {
        var user    = RequestUserContextMock.ForUser().AsGlobalAdmin().Object;
        var handler = CreateHandler(user);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithPrivilege_PersistsSprintAndReturnsDto()
    {
        var user    = RequestUserContextMock.ForUser()
                          .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;
        var handler = CreateHandler(user);
        var command = ValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be(command.Name);
        result.Value.RepositoryId.Should().Be(_repositoryId);
        result.Value.StartDate.Should().Be(command.StartDate);
        result.Value.EndDate.Should().Be(command.EndDate);

        var persisted = await _db.Set<Sprint>().SingleAsync();
        persisted.Id.Should().Be(result.Value.Id);
        persisted.Name.Should().Be(command.Name);
    }

    [Fact]
    public async Task Handle_WhenTodayFallsInsideRange_MarksSprintActive()
    {
        var today   = DateOnly.FromDateTime(DateTime.UtcNow);
        var user    = RequestUserContextMock.ForUser()
                          .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;
        var handler = CreateHandler(user);

        var result = await handler.Handle(
            ValidCommand(today.AddDays(-1), today.AddDays(1)), CancellationToken.None);

        result.Value!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenRangeIsInTheFuture_MarksSprintInactive()
    {
        var today   = DateOnly.FromDateTime(DateTime.UtcNow);
        var user    = RequestUserContextMock.ForUser()
                          .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;
        var handler = CreateHandler(user);

        var result = await handler.Handle(
            ValidCommand(today.AddDays(10), today.AddDays(20)), CancellationToken.None);

        result.Value!.IsActive.Should().BeFalse();
    }

    // ── Overlap rule ────────────────────────────────────────────────────────

    [Theory]
    // new sprint fully inside the existing one
    [InlineData("2026-05-05", "2026-05-08")]
    // new sprint starts before and ends inside
    [InlineData("2026-04-25", "2026-05-05")]
    // new sprint starts inside and ends after
    [InlineData("2026-05-10", "2026-05-20")]
    // new sprint fully contains the existing one
    [InlineData("2026-04-01", "2026-06-01")]
    // boundary: new sprint starts exactly on the existing end date
    [InlineData("2026-05-14", "2026-05-20")]
    // boundary: new sprint ends exactly on the existing start date
    [InlineData("2026-04-20", "2026-05-01")]
    public async Task Handle_WhenDatesOverlapExistingSprint_Fails(string start, string end)
    {
        await SeedSprintAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14));

        var user    = RequestUserContextMock.ForUser()
                          .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;
        var handler = CreateHandler(user);

        var result = await handler.Handle(
            ValidCommand(DateOnly.Parse(start), DateOnly.Parse(end)), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("overlap");
        (await _db.Set<Sprint>().CountAsync()).Should().Be(1, "the existing sprint must be the only one");
    }

    [Theory]
    // entirely before the existing sprint
    [InlineData("2026-04-01", "2026-04-30")]
    // entirely after the existing sprint
    [InlineData("2026-05-15", "2026-05-30")]
    public async Task Handle_WhenDatesDoNotOverlap_Succeeds(string start, string end)
    {
        await SeedSprintAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14));

        var user    = RequestUserContextMock.ForUser()
                          .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;
        var handler = CreateHandler(user);

        var result = await handler.Handle(
            ValidCommand(DateOnly.Parse(start), DateOnly.Parse(end)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<Sprint>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenOverlappingSprintBelongsToAnotherRepository_Succeeds()
    {
        await SeedSprintAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14), Guid.NewGuid());

        var user    = RequestUserContextMock.ForUser()
                          .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;
        var handler = CreateHandler(user);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("the overlap rule is scoped per repository");
    }

    // ── HUB channel provisioning (best-effort) ──────────────────────────────

    [Fact]
    public async Task Handle_WhenHubChannelNotRequested_DoesNotCallHub()
    {
        var user    = RequestUserContextMock.ForUser()
                          .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;
        var handler = CreateHandler(user);

        var result = await handler.Handle(ValidCommand(createHubChannel: false), CancellationToken.None);

        result.Value!.HubChannelId.Should().BeNull();
        result.Value.HubChannelUrl.Should().BeNull();
        _hub.Verify(x => x.FindOrCreateSprintChannelAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenHubChannelRequestedButHubNotConfigured_SkipsProvisioning()
    {
        _settings.SetupGet(x => x.HubChatBaseUrl).Returns(string.Empty);

        var user    = RequestUserContextMock.ForUser()
                          .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;
        var handler = CreateHandler(user);

        var result = await handler.Handle(ValidCommand(createHubChannel: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HubChannelId.Should().BeNull();
        _hub.Verify(x => x.FindOrCreateSprintChannelAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenHubChannelCreated_PersistsLinkAndReturnsUrl()
    {
        var channelId = Guid.NewGuid();
        _settings.SetupGet(x => x.HubChatBaseUrl).Returns("https://hub.test/api");
        _hub.Setup(x => x.FindOrCreateSprintChannelAsync(
                _repositoryId, It.IsAny<Guid>(), "Sprint 1", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelId);

        var user    = RequestUserContextMock.ForUser()
                          .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;
        var handler = CreateHandler(user);

        var result = await handler.Handle(ValidCommand(createHubChannel: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HubChannelId.Should().Be(channelId);
        result.Value.HubChannelUrl.Should().Be($"https://hub.test/channels/{channelId}");

        var link = await _db.Set<SprintChannelLink>().SingleAsync();
        link.SprintId.Should().Be(result.Value.Id);
        link.HubChannelId.Should().Be(channelId);
    }

    [Fact]
    public async Task Handle_WhenHubReturnsNull_StillCreatesSprintWithoutLink()
    {
        _settings.SetupGet(x => x.HubChatBaseUrl).Returns("https://hub.test/api");
        _hub.Setup(x => x.FindOrCreateSprintChannelAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var user    = RequestUserContextMock.ForUser()
                          .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;
        var handler = CreateHandler(user);

        var result = await handler.Handle(ValidCommand(createHubChannel: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HubChannelId.Should().BeNull();
        (await _db.Set<Sprint>().CountAsync()).Should().Be(1);
        (await _db.Set<SprintChannelLink>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenHubThrows_SwallowsErrorAndStillCreatesSprint()
    {
        _settings.SetupGet(x => x.HubChatBaseUrl).Returns("https://hub.test/api");
        _hub.Setup(x => x.FindOrCreateSprintChannelAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("HUB is down"));

        var user    = RequestUserContextMock.ForUser()
                          .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;
        var handler = CreateHandler(user);

        var result = await handler.Handle(ValidCommand(createHubChannel: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("HUB provisioning is best-effort and must never fail sprint creation");
        result.Value!.HubChannelId.Should().BeNull();
        result.Value.HubChannelUrl.Should().BeNull();
        (await _db.Set<Sprint>().CountAsync()).Should().Be(1);
    }
}
