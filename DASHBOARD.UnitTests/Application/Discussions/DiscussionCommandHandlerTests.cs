using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Discussions.Commands.AddDiscussion;
using DASHBOARD.Application.Discussions.Commands.DeleteDiscussion;
using DASHBOARD.Application.Discussions.Commands.UpdateDiscussion;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Discussions;

/// <summary>
/// Unit tests for the discussion commands.
/// </summary>
/// <remarks>
/// Editing and deleting differ on purpose: only the author may rewrite their own words, while a
/// global admin may also remove a comment (moderation). The tests below pin both rules so the
/// two are not "harmonised" into one by mistake.
/// </remarks>
public sealed class DiscussionCommandHandlerTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _authorId     = Guid.NewGuid();
    private readonly Guid                     _taskId       = Guid.NewGuid();

    /// <summary>Sets up an isolated database with one sprint task and the comment author.</summary>
    public DiscussionCommandHandlerTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<SprintTask>().Add(new SprintTask
        {
            RepositoryId = _repositoryId,
            Type         = SprintTaskType.Task,
            Title        = "Task",
        }.WithId(_taskId));
        _db.Set<User>().Add(new User
        {
            Name         = "Author",
            Email        = "author@test.local",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            AvatarClass  = "bg-sky-600",
        }.WithId(_authorId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private AddDiscussionCommandHandler AddHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private UpdateDiscussionCommandHandler UpdateHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private DeleteDiscussionCommandHandler DeleteHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private IRequestUserContext AuthorUser() =>
        RequestUserContextMock.ForUser(_authorId).AsMember(_repositoryId).Object;

    private async Task<DiscussionEntry> AddEntryAsync(
        Guid? authorId = null, Guid? taskId = null, Guid? repositoryId = null)
    {
        var entry = new DiscussionEntry
        {
            SprintTaskId = taskId ?? _taskId,
            RepositoryId = repositoryId ?? _repositoryId,
            AuthorId     = authorId ?? _authorId,
            Body         = "Original comment",
        };
        _db.Set<DiscussionEntry>().Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    // ── Add: access control ─────────────────────────────────────────────────

    [Fact]
    public async Task Add_WhenTheUserIsNotAMember_Throws()
    {
        var handler = AddHandler(RequestUserContextMock.ForUser(_authorId).Object);

        var act = () => handler.Handle(
            new AddDiscussionCommand(_repositoryId, _taskId, "Hello"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Add_WhenTheTaskDoesNotExist_ThrowsNotFound()
    {
        var handler = AddHandler(AuthorUser());

        var act = () => handler.Handle(
            new AddDiscussionCommand(_repositoryId, Guid.NewGuid(), "Hello"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Add_WhenTheTaskBelongsToAnotherRepository_ThrowsNotFound()
    {
        var foreignTaskId = Guid.NewGuid();
        _db.Set<SprintTask>().Add(new SprintTask
        {
            RepositoryId = Guid.NewGuid(),
            Type         = SprintTaskType.Task,
            Title        = "Foreign",
        }.WithId(foreignTaskId));
        await _db.SaveChangesAsync();

        var handler = AddHandler(AuthorUser());

        var act = () => handler.Handle(
            new AddDiscussionCommand(_repositoryId, foreignTaskId, "Hello"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Add: happy path ─────────────────────────────────────────────────────

    [Fact]
    public async Task Add_RecordsTheCommentAgainstTheSignedInAuthor()
    {
        var handler = AddHandler(AuthorUser());

        var result = await handler.Handle(
            new AddDiscussionCommand(_repositoryId, _taskId, "Looks good to me"),
            CancellationToken.None);

        result.AuthorId.Should().Be(_authorId);
        result.AuthorName.Should().Be("Author");
        result.AuthorAvatar.Should().Be("bg-sky-600");
        result.Body.Should().Be("Looks good to me");
        result.UpdatedAt.Should().BeNull("a fresh comment has not been edited");

        var persisted = await _db.Set<DiscussionEntry>().AsNoTracking().SingleAsync();
        persisted.AuthorId.Should().Be(_authorId, "the author comes from the session, not the request");
        persisted.SprintTaskId.Should().Be(_taskId);
    }

    // ── Update: author-only rule ────────────────────────────────────────────

    [Fact]
    public async Task Update_ByTheAuthor_RewritesTheBody()
    {
        var entry   = await AddEntryAsync();
        var handler = UpdateHandler(AuthorUser());

        var result = await handler.Handle(
            new UpdateDiscussionCommand(_repositoryId, _taskId, entry.Id, "Edited comment"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await _db.Set<DiscussionEntry>().AsNoTracking().SingleAsync();
        updated.Body.Should().Be("Edited comment");
        updated.UpdatedAt.Should().NotBeNull("an edited comment is marked as such");
    }

    [Fact]
    public async Task Update_ByAnotherMember_Fails()
    {
        var entry   = await AddEntryAsync();
        var handler = UpdateHandler(
            RequestUserContextMock.ForUser().AsMember(_repositoryId).Object);

        var act = () => handler.Handle(
            new UpdateDiscussionCommand(_repositoryId, _taskId, entry.Id, "Hijacked"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        var unchanged = await _db.Set<DiscussionEntry>().AsNoTracking().SingleAsync();
        unchanged.Body.Should().Be("Original comment");
    }

    [Fact]
    public async Task Update_ByAGlobalAdmin_StillFails()
    {
        var entry   = await AddEntryAsync();
        var handler = UpdateHandler(RequestUserContextMock.ForUser().AsGlobalAdmin().Object);

        var act = () => handler.Handle(
            new UpdateDiscussionCommand(_repositoryId, _taskId, entry.Id, "Rewritten by admin"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Update_WhenTheEntryDoesNotExist_ThrowsNotFound()
    {
        var handler = UpdateHandler(AuthorUser());

        var act = () => handler.Handle(
            new UpdateDiscussionCommand(_repositoryId, _taskId, Guid.NewGuid(), "Edited"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_WhenTheEntryBelongsToAnotherTask_ThrowsNotFound()
    {
        var entry   = await AddEntryAsync(taskId: Guid.NewGuid());
        var handler = UpdateHandler(AuthorUser());

        var act = () => handler.Handle(
            new UpdateDiscussionCommand(_repositoryId, _taskId, entry.Id, "Edited"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Delete: author or admin ─────────────────────────────────────────────

    [Fact]
    public async Task Delete_ByTheAuthor_RemovesTheComment()
    {
        var entry   = await AddEntryAsync();
        var handler = DeleteHandler(AuthorUser());

        var result = await handler.Handle(
            new DeleteDiscussionCommand(_repositoryId, _taskId, entry.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.Set<DiscussionEntry>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_ByAGlobalAdmin_RemovesSomeoneElsesComment()
    {
        var entry   = await AddEntryAsync();
        var handler = DeleteHandler(RequestUserContextMock.ForUser().AsGlobalAdmin().Object);

        var result = await handler.Handle(
            new DeleteDiscussionCommand(_repositoryId, _taskId, entry.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("moderation is an admin capability");
        (await _db.Set<DiscussionEntry>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_ByAnotherMember_Fails()
    {
        var entry   = await AddEntryAsync();
        var handler = DeleteHandler(
            RequestUserContextMock.ForUser().AsMember(_repositoryId).Object);

        var act = () => handler.Handle(
            new DeleteDiscussionCommand(_repositoryId, _taskId, entry.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        (await _db.Set<DiscussionEntry>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Delete_WhenTheEntryDoesNotExist_ThrowsNotFound()
    {
        var handler = DeleteHandler(AuthorUser());

        var act = () => handler.Handle(
            new DeleteDiscussionCommand(_repositoryId, _taskId, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_RemovesOnlyTheNamedComment()
    {
        var target    = await AddEntryAsync();
        var bystander = await AddEntryAsync();
        var handler   = DeleteHandler(AuthorUser());

        await handler.Handle(
            new DeleteDiscussionCommand(_repositoryId, _taskId, target.Id), CancellationToken.None);

        (await _db.Set<DiscussionEntry>().AnyAsync(d => d.Id == bystander.Id)).Should().BeTrue();
    }
}
