using MediatR;
using DASHBOARD.Application.Common.Models;

namespace DASHBOARD.Application.Repositories.Commands.UpdateRepository;

/// <summary>
/// Updates the editable details of a repository. Requires
/// <see cref="DASHBOARD.Domain.Enums.SystemFunction.EditRepository"/>.
/// </summary>
/// <remarks>
/// <c>Code</c> is absent on purpose: it is the URL segment and the work-item key prefix, so
/// changing it would break every link and reference already shared.
/// </remarks>
/// <param name="RepositoryId">The repository to update.</param>
/// <param name="Name">New project name.</param>
/// <param name="Description">New description.</param>
/// <param name="LeadId">
/// The member to hold the Project Manager role. Null leaves the current holder in place. Handing
/// the role over demotes the previous holder to Developer rather than removing them, so their
/// history and assignments stay intact.
/// </param>
public sealed record UpdateRepositoryCommand(
    Guid   RepositoryId,
    string Name,
    string Description,
    Guid?  LeadId = null) : IRequest<Result>;
