using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Sprints.Commands.DeleteSprint;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Tests.Application.Sprints.Commands;

/// <summary>Unit tests for <see cref="DeleteSprintCommandHandler"/>.</summary>
public sealed class DeleteSprintCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();

    /// <summary>Sets up an isolated in-memory database.</summary>
    public DeleteSprintCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private DeleteSprintCommandHandler CreateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorizedUser() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.ManageSprint, _repositoryId).Object;

    /// <summary>Adds a sprint that is safely in the past, so the active-sprint rule does not interfere.</summary>
    private async Task<Sprint> SeedPastSprintAsync(Guid? repositoryId = null)
    {
        var today  = DateOnly.FromDateTime(DateTime.UtcNow);
        var sprint = new Sprint
        {
            RepositoryId = repositoryId ?? _repositoryId,
            Name         = "Past Sprint",
            StartDate    = today.AddDays(-30),
            EndDate      = today.AddDays(-16),
        };
        _db.Set<Sprint>().Add(sprint);
        await _db.SaveChangesAsync();
        return sprint;
    }

    // ── Permission ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutManageSprintPrivilege_FailsAndKeepsSprint()
    {
        var sprint  = await SeedPastSprintAsync();
        var handler = CreateHandler(RequestUserContextMock.ForUser().Object);

        var result = await handler.Handle(
            new DeleteSprintCommand(_repositoryId, sprint.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("permission");
        (await _db.Set<Sprint>().CountAsync()).Should().Be(1);
    }

    // ── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSprintDoesNotExist_ThrowsNotFound()
    {
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new DeleteSprintCommand(_repositoryId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenSprintBelongsToAnotherRepository_ThrowsNotFound()
    {
        var sprint  = await SeedPastSprintAsync(Guid.NewGuid());
        var handler = CreateHandler(AuthorizedUser());

        var act = () => handler.Handle(
            new DeleteSprintCommand(_repositoryId, sprint.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>(
            "a sprint must not be reachable through a repository that does not own it");
    }

    // ── Active-sprint rule ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSprintIsActive_Fails()
    {
        var today  = DateOnly.FromDateTime(DateTime.UtcNow);
        var sprint = new Sprint
        {
            RepositoryId = _repositoryId,
            Name         = "Active Sprint",
            StartDate    = today.AddDays(-1),
            EndDate      = today.AddDays(1),
        };
        _db.Set<Sprint>().Add(sprint);
        await _db.SaveChangesAsync();

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DeleteSprintCommand(_repositoryId, sprint.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("active sprint");
        (await _db.Set<Sprint>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenSprintIsEntirelyInTheFuture_Succeeds()
    {
        var today  = DateOnly.FromDateTime(DateTime.UtcNow);
        var sprint = new Sprint
        {
            RepositoryId = _repositoryId,
            Name         = "Future Sprint",
            StartDate    = today.AddDays(10),
            EndDate      = today.AddDays(20),
        };
        _db.Set<Sprint>().Add(sprint);
        await _db.SaveChangesAsync();

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DeleteSprintCommand(_repositoryId, sprint.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<Sprint>().CountAsync()).Should().Be(0);
    }

    // ── Committed-task rule ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSprintHasCommittedTasks_FailsAndReportsCount()
    {
        var sprint = await SeedPastSprintAsync();
        _db.Set<SprintTask>().AddRange(
            new SprintTask { SprintId = sprint.Id, RepositoryId = _repositoryId, Title = "Task A" },
            new SprintTask { SprintId = sprint.Id, RepositoryId = _repositoryId, Title = "Task B" });
        await _db.SaveChangesAsync();

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DeleteSprintCommand(_repositoryId, sprint.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("2 committed task(s)");
        (await _db.Set<Sprint>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTasksBelongToAnotherSprint_Succeeds()
    {
        var sprint      = await SeedPastSprintAsync();
        var otherSprint = await SeedPastSprintAsync();
        _db.Set<SprintTask>().Add(
            new SprintTask { SprintId = otherSprint.Id, RepositoryId = _repositoryId, Title = "Other" });
        await _db.SaveChangesAsync();

        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DeleteSprintCommand(_repositoryId, sprint.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("the task rule is scoped to the sprint being deleted");
    }

    // ── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNoTasksAndNotActive_DeletesSprint()
    {
        var sprint  = await SeedPastSprintAsync();
        var handler = CreateHandler(AuthorizedUser());

        var result = await handler.Handle(
            new DeleteSprintCommand(_repositoryId, sprint.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<Sprint>().AnyAsync(s => s.Id == sprint.Id)).Should().BeFalse();
    }
}
