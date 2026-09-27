using DASHBOARD.Application.Common.Exceptions;
using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Application.Discussions.Queries.ListDiscussions;
using DASHBOARD.Application.Invitations.Commands.RevokeInvitation;
using DASHBOARD.Application.Invitations.Queries.ListInvitations;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.UnitTests.Common;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.UnitTests.Application.Invitations;

/// <summary>
/// Unit tests for <see cref="RevokeInvitationCommandHandler"/>, <see cref="ListInvitationsQueryHandler"/>
/// and <see cref="ListDiscussionsQueryHandler"/>.
/// </summary>
public sealed class InvitationListingAndRevokeTests : IDisposable
{
    private readonly TestDatabase             _database;
    private readonly TestApplicationDbContext _db;
    private readonly Guid                     _repositoryId = Guid.NewGuid();
    private readonly Guid                     _taskId       = Guid.NewGuid();

    /// <summary>
    /// Sets up an isolated database holding the owning repository and one work item.
    /// The repository row is required: <c>Invitation</c> declares a required relationship to it,
    /// so an invitation pointing at a missing repository is filtered out of query results.
    /// </summary>
    public InvitationListingAndRevokeTests()
    {
        _database = TestDbContextFactory.Create();
        _db       = _database.Db;

        _db.Set<Repository>().Add(
            new Repository { Name = "Dashboard", Code = "DASH" }.WithId(_repositoryId));
        _db.Set<SprintTask>().Add(new SprintTask
        {
            RepositoryId = _repositoryId,
            Type         = SprintTaskType.Task,
            Title        = "Task",
        }.WithId(_taskId));
        _db.SaveChanges();
    }

    /// <summary>Disposes the in-memory database.</summary>
    public void Dispose() => _database.Dispose();

    private RevokeInvitationCommandHandler RevokeHandler(IRequestUserContext user) =>
        new(_db, user, _database.Uow);

    private ListInvitationsQueryHandler InvitationsHandler(IRequestUserContext user) => new(_db, user);

    private ListDiscussionsQueryHandler DiscussionsHandler(IRequestUserContext user) => new(_db, user);

    private IRequestUserContext Inviter() =>
        RequestUserContextMock.ForUser()
            .WithPrivilege(SystemFunction.InviteMembers, _repositoryId).Object;

    private IRequestUserContext MemberUser() =>
        RequestUserContextMock.ForUser().AsMember(_repositoryId).Object;

    private async Task<User> AddUserAsync(string name)
    {
        var user = new User
        {
            Name         = name,
            Email        = $"{name.ToLowerInvariant()}@test.local",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            AvatarClass  = "bg-sky-600",
        };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Role> AddRoleAsync(string name = "Developer")
    {
        var role = new Role
        {
            Name             = name,
            RepositoryId     = _repositoryId,
            AllowedFunctions = [SystemFunction.ViewRepository],
        };
        _db.Set<Role>().Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    /// <summary>
    /// Adds an invitation. When no inviter is supplied one is created, because the relationship
    /// to <c>InvitedBy</c> is required — an invitation naming a non-existent user is not a state
    /// the database can hold.
    /// </summary>
    private async Task<Invitation> AddInvitationAsync(
        Guid             roleId,
        string           email        = "invitee@external.local",
        InvitationStatus status       = InvitationStatus.Pending,
        Guid?            invitedById  = null,
        Guid?            repositoryId = null,
        string           tokenHash    = "hash")
    {
        var inviterId = invitedById ?? (await AddUserAsync($"Inviter{Guid.NewGuid():N}")).Id;

        var invitation = new Invitation
        {
            Email           = email,
            RepositoryId    = repositoryId ?? _repositoryId,
            InvitedByUserId = inviterId,
            RoleId          = roleId,
            DefaultRole     = "Developer",
            TokenHash       = tokenHash,
            ExpiresAt       = DateTime.UtcNow.AddHours(24),
            Status          = status,
        };
        _db.Set<Invitation>().Add(invitation);
        await _db.SaveChangesAsync();
        return invitation;
    }

    private async Task<DiscussionEntry> AddCommentAsync(
        Guid authorId, string body = "A comment", Guid? taskId = null, Guid? repositoryId = null)
    {
        var entry = new DiscussionEntry
        {
            SprintTaskId = taskId ?? _taskId,
            RepositoryId = repositoryId ?? _repositoryId,
            AuthorId     = authorId,
            Body         = body,
        };
        _db.Set<DiscussionEntry>().Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    // ── RevokeInvitation ────────────────────────────────────────────────────

    [Fact]
    public async Task Revoke_WithoutInviteMembersPrivilege_FailsAndKeepsItPending()
    {
        var role       = await AddRoleAsync();
        var invitation = await AddInvitationAsync(role.Id);

        var act = () => RevokeHandler(MemberUser()).Handle(
            new RevokeInvitationCommand(_repositoryId, invitation.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        var unchanged = await _db.Set<Invitation>().AsNoTracking().SingleAsync();
        unchanged.Status.Should().Be(InvitationStatus.Pending);
    }

    [Fact]
    public async Task Revoke_WhenTheInvitationDoesNotExist_ThrowsNotFound()
    {
        var act = () => RevokeHandler(Inviter()).Handle(
            new RevokeInvitationCommand(_repositoryId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Revoke_WhenTheInvitationBelongsToAnotherRepository_ThrowsNotFound()
    {
        var role    = await AddRoleAsync();
        var foreign = await AddInvitationAsync(role.Id, repositoryId: Guid.NewGuid());

        var act = () => RevokeHandler(Inviter()).Handle(
            new RevokeInvitationCommand(_repositoryId, foreign.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(InvitationStatus.Accepted)]
    [InlineData(InvitationStatus.Revoked)]
    [InlineData(InvitationStatus.Expired)]
    public async Task Revoke_WhenTheInvitationIsNoLongerPending_Fails(InvitationStatus status)
    {
        var role       = await AddRoleAsync();
        var invitation = await AddInvitationAsync(role.Id, status: status);

        var result = await RevokeHandler(Inviter()).Handle(
            new RevokeInvitationCommand(_repositoryId, invitation.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue("there is nothing left to withdraw");
        result.Error.Should().Contain("Only pending invitations");

        var unchanged = await _db.Set<Invitation>().AsNoTracking().SingleAsync();
        unchanged.Status.Should().Be(status, "an accepted invite must not be rewritten as revoked");
    }

    [Fact]
    public async Task Revoke_APendingInvitation_MarksItRevoked()
    {
        var role       = await AddRoleAsync();
        var invitation = await AddInvitationAsync(role.Id);

        var result = await RevokeHandler(Inviter()).Handle(
            new RevokeInvitationCommand(_repositoryId, invitation.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var revoked = await _db.Set<Invitation>().AsNoTracking().SingleAsync();
        revoked.Status.Should().Be(InvitationStatus.Revoked);
    }

    [Fact]
    public async Task Revoke_LeavesOtherInvitationsAlone()
    {
        var role      = await AddRoleAsync();
        var target    = await AddInvitationAsync(role.Id, "one@external.local", tokenHash: "a");
        var bystander = await AddInvitationAsync(role.Id, "two@external.local", tokenHash: "b");

        await RevokeHandler(Inviter()).Handle(
            new RevokeInvitationCommand(_repositoryId, target.Id), CancellationToken.None);

        var untouched = await _db.Set<Invitation>().AsNoTracking()
            .SingleAsync(i => i.Id == bystander.Id);
        untouched.Status.Should().Be(InvitationStatus.Pending);
    }

    // ── ListInvitations ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListInvitations_WithoutInviteMembersPrivilege_Throws()
    {
        var act = () => InvitationsHandler(MemberUser()).Handle(
            new ListInvitationsQuery(_repositoryId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>(
            "invited email addresses are not ordinary member-visible data");
    }

    [Fact]
    public async Task ListInvitations_WithNone_ReturnsEmpty()
    {
        var result = await InvitationsHandler(Inviter()).Handle(
            new ListInvitationsQuery(_repositoryId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListInvitations_ReturnsOnlyInvitationsOfThisRepository()
    {
        var foreignRepoId = Guid.NewGuid();
        _db.Set<Repository>().Add(
            new Repository { Name = "Other", Code = "OTH" }.WithId(foreignRepoId));
        await _db.SaveChangesAsync();

        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id, "mine@external.local",   tokenHash: "a");
        await AddInvitationAsync(role.Id, "foreign@external.local", tokenHash: "b",
            repositoryId: foreignRepoId);

        var result = await InvitationsHandler(Inviter()).Handle(
            new ListInvitationsQuery(_repositoryId), CancellationToken.None);

        result.Should().ContainSingle().Which.Email.Should().Be("mine@external.local");
    }

    [Fact]
    public async Task ListInvitations_ShowsEveryStatusNotJustPending()
    {
        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id, "a@external.local", InvitationStatus.Pending,  tokenHash: "a");
        await AddInvitationAsync(role.Id, "b@external.local", InvitationStatus.Accepted, tokenHash: "b");
        await AddInvitationAsync(role.Id, "c@external.local", InvitationStatus.Revoked,  tokenHash: "c");

        var result = await InvitationsHandler(Inviter()).Handle(
            new ListInvitationsQuery(_repositoryId), CancellationToken.None);

        result.Should().HaveCount(3, "the admin screen shows invite history, not only live invites");
    }

    [Fact]
    public async Task ListInvitations_ProjectsTheInviterAndRoleName()
    {
        var inviter    = await AddUserAsync("Alice");
        var role       = await AddRoleAsync("QA Lead");
        var invitation = await AddInvitationAsync(role.Id, invitedById: inviter.Id);

        var result = await InvitationsHandler(Inviter()).Handle(
            new ListInvitationsQuery(_repositoryId), CancellationToken.None);

        var dto = result.Single();
        dto.Id.Should().Be(invitation.Id);
        dto.InvitedByName.Should().Be("Alice");
        dto.RoleName.Should().Be("QA Lead");
        dto.DefaultRole.Should().Be("Developer");
        dto.ExpiresAt.Should().BeCloseTo(invitation.ExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ListInvitations_ReturnsNewestFirst()
    {
        var role = await AddRoleAsync();
        await AddInvitationAsync(role.Id, "older@external.local", tokenHash: "a");
        await AddInvitationAsync(role.Id, "newer@external.local", tokenHash: "b");

        var result = await InvitationsHandler(Inviter()).Handle(
            new ListInvitationsQuery(_repositoryId), CancellationToken.None);

        // The most recent invite is what an admin is usually chasing.
        result.Select(i => i.Email).Should().ContainInOrder(
            "newer@external.local", "older@external.local");
    }

    // ── ListDiscussions ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListDiscussions_WhenTheUserIsNotAMember_Throws()
    {
        var act = () => DiscussionsHandler(RequestUserContextMock.ForUser().Object).Handle(
            new ListDiscussionsQuery(_repositoryId, _taskId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ListDiscussions_WithNoComments_ReturnsEmpty()
    {
        var result = await DiscussionsHandler(MemberUser()).Handle(
            new ListDiscussionsQuery(_repositoryId, _taskId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListDiscussions_ReturnsCommentsOldestFirst()
    {
        var alice = await AddUserAsync("Alice");
        await AddCommentAsync(alice.Id, "First");
        await AddCommentAsync(alice.Id, "Second");

        var result = await DiscussionsHandler(MemberUser()).Handle(
            new ListDiscussionsQuery(_repositoryId, _taskId), CancellationToken.None);

        // A conversation reads top to bottom, so the oldest comment leads.
        result.Select(d => d.Body).Should().ContainInOrder("First", "Second");
    }

    [Fact]
    public async Task ListDiscussions_ProjectsTheAuthorProfile()
    {
        var alice = await AddUserAsync("Alice");
        await AddCommentAsync(alice.Id, "Looks good");

        var result = await DiscussionsHandler(MemberUser()).Handle(
            new ListDiscussionsQuery(_repositoryId, _taskId), CancellationToken.None);

        var dto = result.Single();
        dto.AuthorId.Should().Be(alice.Id);
        dto.AuthorName.Should().Be("Alice");
        dto.AuthorAvatar.Should().Be("bg-sky-600");
        dto.Body.Should().Be("Looks good");
        dto.UpdatedAt.Should().BeNull("an unedited comment carries no edit marker");
    }

    [Fact]
    public async Task ListDiscussions_ReturnsOnlyCommentsOfTheRequestedWorkItem()
    {
        var otherTaskId = Guid.NewGuid();
        _db.Set<SprintTask>().Add(new SprintTask
        {
            RepositoryId = _repositoryId,
            Type         = SprintTaskType.Task,
            Title        = "Other",
        }.WithId(otherTaskId));
        await _db.SaveChangesAsync();

        var alice = await AddUserAsync("Alice");
        await AddCommentAsync(alice.Id, "On this task");
        await AddCommentAsync(alice.Id, "On another task", taskId: otherTaskId);

        var result = await DiscussionsHandler(MemberUser()).Handle(
            new ListDiscussionsQuery(_repositoryId, _taskId), CancellationToken.None);

        result.Should().ContainSingle().Which.Body.Should().Be("On this task");
    }

    [Fact]
    public async Task ListDiscussions_ReturnsOnlyCommentsOfTheRequestedRepository()
    {
        var alice = await AddUserAsync("Alice");
        await AddCommentAsync(alice.Id, "Mine");
        await AddCommentAsync(alice.Id, "Foreign", repositoryId: Guid.NewGuid());

        var result = await DiscussionsHandler(MemberUser()).Handle(
            new ListDiscussionsQuery(_repositoryId, _taskId), CancellationToken.None);

        result.Should().ContainSingle().Which.Body.Should().Be("Mine");
    }
}
