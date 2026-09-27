using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Sprints.Commands.UpdateSprint;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Sprints.Commands;

/// <summary>Unit tests for <see cref="UpdateSprintCommandHandler"/>.</summary>
public sealed class UpdateSprintCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public UpdateSprintCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private UpdateSprintCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;

    private async Task<Sprint> SeedSprintAsync(
        DateOnly start, DateOnly end, string name = "Original", Guid? repositoryId = null)
    {
        var sprint = new Sprint
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Name         = name,
            StartDate    = start,
            EndDate      = end,
        };
        _db.Set<Sprint>().Add(sprint);
        await _db.SaveChangesAsync();
        return sprint;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageSprintPrivilege_FailsAndLeavesSprintUnchanged()
    {
        var sprint  = await SeedSprintAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14));
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var act = () => handler.Handle(
            new UpdateSprintCommand(_repositoryId, sprint.Id, "Renamed",
                new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 14)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        var unchanged = await _db.Set<Sprint>().AsNoTracking().SingleAsync();
        unchanged.Name.Should().Be("Original");
    }

    // ── Date-order rule ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenEndDatePrecedesStartDate_Fails()
    {
        var sprint  = await SeedSprintAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14));
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new UpdateSprintCommand(_repositoryId, sprint.Id, "Renamed",
                new DateOnly(2026, 6, 14), new DateOnly(2026, 6, 1)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("EndDate must be after StartDate.");
    }

    [Fact]
    public async Task Handle_WhenEndDateEqualsStartDate_Fails()
    {
        var sprint  = await SeedSprintAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14));
        var handler = CreateHandler(AuthorizedUser());
        var sameDay = new DateOnly(2026, 6, 1);

        var result = await handler.Handle(
            new UpdateSprintCommand(_repositoryId, sprint.Id, "Renamed", sameDay, sameDay),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("EndDate must be after StartDate.");
    }

    // ── Overlap rule ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNewDatesOverlapAnotherSprint_Fails()
    {
        var target = await SeedSprintAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14));
        await SeedSprintAsync(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 14), "Neighbour");

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new UpdateSprintCommand(_repositoryId, target.Id, "Renamed",
                new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 20)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("overlap");
    }

    [Fact]
    public async Task Handle_WhenDatesOverlapOnlyItself_Succeeds()
    {
        var sprint  = await SeedSprintAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14));
        var handler = CreateHandler(AuthorizedUser());

        // Keeping the same range must not trip the overlap check against the sprint's own row.
        var result = await handler.Handle(
            new UpdateSprintCommand(_repositoryId, sprint.Id, "Renamed",
                new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenOverlappingSprintBelongsToAnotherRepository_Succeeds()
    {
        var target = await SeedSprintAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14));
        await SeedSprintAsync(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 14),
            "Foreign", Guid.NewGuid());

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new UpdateSprintCommand(_repositoryId, target.Id, "Renamed",
                new DateOnly(2026, 6, 5), new DateOnly(2026, 6, 20)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("the overlap rule is scoped per repository");
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSprintDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new UpdateSprintCommand(_repositoryId, Guid.NewGuid(), "Renamed",
                new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14)),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenSprintBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreign = await SeedSprintAsync(
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14), "Foreign", Guid.NewGuid());

        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new UpdateSprintCommand(_repositoryId, foreign.Id, "Renamed",
                new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 14)),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidChanges_UpdatesNameDatesAndTimestamp()
    {
        var sprint  = await SeedSprintAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 14));
        sprint.UpdatedAt.Should().BeNull();

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new UpdateSprintCommand(_repositoryId, sprint.Id, "Renamed Sprint",
                new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 14)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _db.Set<Sprint>().AsNoTracking().SingleAsync();
        updated.Name.Should().Be("Renamed Sprint");
        updated.StartDate.Should().Be(new DateOnly(2026, 6, 1));
        updated.EndDate.Should().Be(new DateOnly(2026, 6, 14));
        updated.UpdatedAt.Should().NotBeNull("Touch() must stamp the update time");
    }
}
