using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Enums;
using Moq;

namespace DASHBOARD.Tests.Common;

/// <summary>
/// Fluent builder for <see cref="IRequestUserContext"/> mocks, so permission setup in handler tests
/// reads as one line instead of five <c>Setup</c> calls.
/// </summary>
public sealed class RequestUserContextMock
{
    private readonly Mock<IRequestUserContext> _mock = new(MockBehavior.Loose);

    private RequestUserContextMock(Guid userId, Guid orgId)
    {
        _mock.SetupGet(x => x.UserId).Returns(userId);
        _mock.SetupGet(x => x.OrgId).Returns(orgId);
        _mock.Setup(x => x.IsSelf(It.IsAny<Guid>()))
             .Returns<Guid>(target => target == userId);
    }

    /// <summary>Starts a builder for a user who is denied every privilege by default.</summary>
    /// <param name="userId">The authenticated user id; a new GUID when omitted.</param>
    /// <param name="orgId">The user's organization id; a new GUID when omitted.</param>
    /// <returns>A builder with all permission checks returning <c>false</c>.</returns>
    public static RequestUserContextMock ForUser(Guid? userId = null, Guid? orgId = null)
    {
        var builder = new RequestUserContextMock(userId ?? Guid.NewGuid(), orgId ?? Guid.NewGuid());
        builder._mock.Setup(x => x.IsGlobalAdminAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        builder._mock.Setup(x => x.IsMemberOfAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        builder._mock.Setup(x => x.CanAsync(It.IsAny<Guid>(), It.IsAny<SystemFunction>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        builder._mock.Setup(x => x.CanManageUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        return builder;
    }

    /// <summary>Grants the given privilege — on one repository, or on all when none is specified.</summary>
    /// <param name="privilege">The privilege to grant.</param>
    /// <param name="repositoryId">Restrict the grant to this repository; omit to grant everywhere.</param>
    /// <returns>This builder, for chaining.</returns>
    public RequestUserContextMock WithPrivilege(SystemFunction privilege, Guid? repositoryId = null)
    {
        // It.IsAny is only recognised as a matcher when it appears literally inside the expression
        // tree — hoisting it into a variable silently yields default(Guid) and matches nothing.
        if (repositoryId is { } id)
            _mock.Setup(x => x.CanAsync(id, privilege, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
        else
            _mock.Setup(x => x.CanAsync(It.IsAny<Guid>(), privilege, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        return this;
    }

    /// <summary>Makes the user a member — of one repository, or of all when none is specified.</summary>
    /// <param name="repositoryId">Restrict membership to this repository; omit for all.</param>
    /// <returns>This builder, for chaining.</returns>
    public RequestUserContextMock AsMember(Guid? repositoryId = null)
    {
        if (repositoryId is { } id)
            _mock.Setup(x => x.IsMemberOfAsync(id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
        else
            _mock.Setup(x => x.IsMemberOfAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        return this;
    }

    /// <summary>Makes the user a global admin, which also grants every privilege and membership.</summary>
    /// <returns>This builder, for chaining.</returns>
    public RequestUserContextMock AsGlobalAdmin()
    {
        _mock.Setup(x => x.IsGlobalAdminAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mock.Setup(x => x.IsMemberOfAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mock.Setup(x => x.CanAsync(It.IsAny<Guid>(), It.IsAny<SystemFunction>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        // A global admin may manage anyone but themselves.
        var selfId = _mock.Object.UserId;
        _mock.Setup(x => x.CanManageUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Guid target, CancellationToken _) => target != selfId);
        return this;
    }

    /// <summary>Gets the underlying mock, for asserting calls.</summary>
    public Mock<IRequestUserContext> Mock => _mock;

    /// <summary>Gets the configured <see cref="IRequestUserContext"/> to pass into a handler.</summary>
    public IRequestUserContext Object => _mock.Object;
}
