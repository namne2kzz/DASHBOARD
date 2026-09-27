using DASHBOARD.Application.Common.Interfaces;
using DASHBOARD.Domain.Constants;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using DASHBOARD.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DASHBOARD.IntegrationTests.Infrastructure;

/// <summary>
/// One organization with a repository, a set of roles and a signed-in user — the minimum a request
/// needs in order to get past authentication and authorisation.
/// </summary>
/// <param name="OrgId">The organization (tenant).</param>
/// <param name="OrgAlias">The alias used on the login endpoint.</param>
/// <param name="RepositoryId">A repository inside that organization.</param>
/// <param name="RepositoryCode">The repository's work-item prefix.</param>
/// <param name="UserId">The seeded user.</param>
/// <param name="Email">The user's login email.</param>
/// <param name="Password">The user's plaintext password.</param>
public sealed record TenantSeed(
    Guid   OrgId,
    string OrgAlias,
    Guid   RepositoryId,
    string RepositoryCode,
    Guid   UserId,
    string Email,
    string Password);

/// <summary>Seeds tenants directly through the application's own services.</summary>
/// <remarks>
/// Passwords go through the real <see cref="IPasswordService"/> so the seeded user can actually log
/// in — hand-written hashes would never verify. Everything else is written straight to the context,
/// which keeps arrange code short and independent of whichever endpoint is under test.
/// </remarks>
public static class TestData
{
    /// <summary>
    /// Creates an organization, a repository with its default roles, and a member of that repository.
    /// </summary>
    /// <param name="factory">The running API, used for its service provider.</param>
    /// <param name="alias">Organization alias; must be unique across the test run.</param>
    /// <param name="repositoryCode">Repository code; must be unique inside the organization.</param>
    /// <param name="isGlobalAdmin">Whether the seeded user is a global admin.</param>
    /// <param name="roleName">Which default role the user holds in the repository.</param>
    /// <returns>Identifiers and credentials for the seeded tenant.</returns>
    public static async Task<TenantSeed> SeedTenantAsync(
        ApiFactory factory,
        string     alias,
        string     repositoryCode  = "DASH",
        bool       isGlobalAdmin   = false,
        string     roleName        = DefaultRoleDefinitions.ScrumMaster)
    {
        using var scope = factory.Services.CreateScope();
        var db        = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        const string password = "Str0ng!Pass";
        var (hash, salt) = passwords.HashPassword(password);

        var org = new Organization
        {
            Name                = alias,
            Alias               = alias,
            ContactEmail        = $"admin@{alias}.local",
            LicenseKey          = $"LICENSE-{alias}",
            LicenseDueDate      = DateTime.UtcNow.AddYears(1),
            LicenseRepoCapacity = 50,
        };
        db.Organizations.Add(org);

        var user = new User
        {
            OrgId         = org.Id,
            Name          = $"{alias} user",
            Email         = $"user@{alias}.local",
            PasswordHash  = hash,
            PasswordSalt  = salt,
            IsGlobalAdmin = isGlobalAdmin,
            AuthProvider  = AuthProvider.System,
        };
        db.Users.Add(user);

        var repository = new Repository
        {
            OrgId       = org.Id,
            Name        = $"{alias} project",
            Code        = repositoryCode,
            Description = "Seeded by integration tests",
        };
        db.Repositories.Add(repository);

        // Give the repository its own copy of the standard roles, exactly as CreateRepository does.
        Role? assignedRole = null;
        foreach (var definition in DefaultRoleDefinitions.All)
        {
            var role = new Role
            {
                RepositoryId     = repository.Id,
                IsDefault        = true,
                Name             = definition.Name,
                Description      = definition.Description,
                AllowedFunctions = [.. definition.AllowedFunctions],
            };
            db.Roles.Add(role);

            if (definition.Name == roleName)
                assignedRole = role;
        }

        db.RepositoryMembers.Add(new RepositoryMember
        {
            UserId       = user.Id,
            RepositoryId = repository.Id,
            DefaultRole  = roleName,
            RoleId       = assignedRole!.Id,
        });

        await db.SaveChangesAsync();

        return new TenantSeed(
            org.Id, alias, repository.Id, repositoryCode, user.Id, user.Email, password);
    }
}
