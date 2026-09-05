using System.Security.Cryptography;
using System.Text;
using DASHBOARD.Domain.Constants;
using DASHBOARD.Domain.Entities;
using DASHBOARD.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DASHBOARD.Infrastructure.Persistence;

/// <summary>
/// Static seed data applied via EF Core <c>HasData</c>.
/// Passwords use PBKDF2-SHA512 with a random salt (see <c>PasswordService</c> in Step 3 for hashing).
/// Placeholder hashes below — run <c>dotnet ef migrations add InitialSeed</c> after replacing with real hashes.
/// </summary>
public static class SeedData
{
    // ── Fixed GUIDs ───────────────────────────────────────────────────────────
    public static readonly Guid DefaultOrgId   = new("00000000-0000-0000-0009-000000000001");

    public static readonly Guid AdminUserId    = new("00000000-0000-0000-0001-000000000001");
    public static readonly Guid DevUserId      = new("00000000-0000-0000-0001-000000000002");

    public static readonly Guid DashRepoId     = new("00000000-0000-0000-0002-000000000001");

    public static readonly Guid DevRoleId      = new("00000000-0000-0000-0003-000000000001");

    // ── Demo repo's default roles (RepositoryId = DashRepoId, IsDefault = true) ──
    // Fixed ids are seed/demo-only; no business code references them.
    public static readonly Guid ScrumMasterRoleId    = new("00000000-0000-0000-0003-000000000010");
    public static readonly Guid ProjectManagerRoleId = new("00000000-0000-0000-0003-000000000011");
    public static readonly Guid DeveloperRoleId       = new("00000000-0000-0000-0003-000000000012");
    public static readonly Guid TesterRoleId          = new("00000000-0000-0000-0003-000000000013");
    public static readonly Guid BusinessAnalystRoleId = new("00000000-0000-0000-0003-000000000014");

    public static readonly Guid AdminMemberId  = new("00000000-0000-0000-0004-000000000001");
    public static readonly Guid DevMemberId    = new("00000000-0000-0000-0004-000000000002");

    public static readonly Guid Story1Id        = new("00000000-0000-0000-0005-000000000001");
    public static readonly Guid Bug1Id          = new("00000000-0000-0000-0005-000000000002");
    public static readonly Guid Task1Id         = new("00000000-0000-0000-0005-000000000003");
    public static readonly Guid Improvement1Id  = new("00000000-0000-0000-0005-000000000004");
    public static readonly Guid TestPlan1Id     = new("00000000-0000-0000-0005-000000000005");


    public static readonly Guid Sprint1Id      = new("00000000-0000-0000-0007-000000000001");

    public static readonly Guid BacklogEpic1Id = new("00000000-0000-0000-0008-000000000001");
    public static readonly Guid BacklogUs1Id   = new("00000000-0000-0000-0008-000000000002");

    private static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // ── Deterministic seed passwords (PBKDF2-SHA512, 310 000 iterations) ─────
    // Fixed salts ensure migration snapshots are stable across regenerations.
    // Default password for ALL seed users: "Password123!"
    // DO NOT use in production — replace via admin panel after first deployment.
    private const int    SeedIterations = 310_000;
    private const int    SeedHashSize   = 64;
    private static readonly byte[] AdminSeedSalt = new byte[32]; // 32 zeros
    private static readonly byte[] DevSeedSalt   = Enumerable.Repeat((byte)1, 32).ToArray(); // 32 ones

    private static readonly string AdminHash = Convert.ToBase64String(
        Rfc2898DeriveBytes.Pbkdf2("Password123!", AdminSeedSalt, SeedIterations, HashAlgorithmName.SHA512, SeedHashSize));
    private static readonly string DevHash = Convert.ToBase64String(
        Rfc2898DeriveBytes.Pbkdf2("Password123!", DevSeedSalt, SeedIterations, HashAlgorithmName.SHA512, SeedHashSize));
    private static readonly string AdminSaltB64 = Convert.ToBase64String(AdminSeedSalt);
    private static readonly string DevSaltB64   = Convert.ToBase64String(DevSeedSalt);

    /// <summary>Applies all seed data to the model builder.</summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    public static void Apply(ModelBuilder modelBuilder)
    {
        SeedOrg(modelBuilder);
        SeedUsers(modelBuilder);
        SeedRepository(modelBuilder);
        SeedMetadata(modelBuilder);
        SeedRolesAndMembers(modelBuilder);
        SeedItems(modelBuilder);
        SeedSprint(modelBuilder);
        SeedBacklog(modelBuilder);
        SeedSmartBoard(modelBuilder);
    }

    // ── Organization (default tenant for pre-existing data) ────────────────────
    private static void SeedOrg(ModelBuilder mb)
    {
        mb.Entity<Organization>().HasData(new
        {
            Id = DefaultOrgId,
            Name = "Default Organization",
            Alias = "default",
            ContactEmail = "admin@dashboard.local",
            About = "Default organization created for pre-existing repositories and users.",
            // Consumes license key #1 from appsettings (marked active: true there).
            LicenseKey = "TkVYLTAwMDEtUFJPRA==",
            LicenseDueDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            LicenseExpireDate = new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            LicenseRepoCapacity = 20,
            CreatedAt = SeedDate,
            UpdatedAt = (DateTime?)null,
        });
    }

    // ── Users ─────────────────────────────────────────────────────────────────
    private static void SeedUsers(ModelBuilder mb)
    {
        mb.Entity<User>().HasData(
            new
            {
                Id = AdminUserId,
                OrgId = DefaultOrgId,
                Name = "Admin User",
                Email = "admin@dashboard.local",
                PasswordHash = AdminHash,
                PasswordSalt = AdminSaltB64,
                AvatarClass = "bg-sky-600",
                IsGlobalAdmin = true,
                AuthProvider = AuthProvider.System,
                GoogleSubjectId = (string?)null,
                ManagerId = (Guid?)null,
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
                IsDeleted = false,
                DeletedAt = (DateTime?)null,
                DeletedByUserId = (Guid?)null,
            },
            new
            {
                Id = DevUserId,
                OrgId = DefaultOrgId,
                Name = "Dev User",
                Email = "dev@dashboard.local",
                PasswordHash = DevHash,
                PasswordSalt = DevSaltB64,
                AvatarClass = "bg-violet-600",
                IsGlobalAdmin = false,
                AuthProvider = AuthProvider.System,
                GoogleSubjectId = (string?)null,
                ManagerId = AdminUserId,
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
                IsDeleted = false,
                DeletedAt = (DateTime?)null,
                DeletedByUserId = (Guid?)null,
            });
    }

    // ── Repository ────────────────────────────────────────────────────────────
    private static void SeedRepository(ModelBuilder mb)
    {
        mb.Entity<Repository>().HasData(new
        {
            Id = DashRepoId,
            OrgId = DefaultOrgId,
            Name = "Dashboard Project",
            Code = "DASH",
            Description = "Main project repository for the DASHBOARD application.",
            CreatedAt = SeedDate,
            UpdatedAt = (DateTime?)null,
        });
    }

    // ── Demo repo's metadata catalog (RepoRole disciplines) ───────────────────
    private static void SeedMetadata(ModelBuilder mb)
    {
        // RepoRole entries scoped to the demo repository (one row per default-role name).
        var disciplines = new[]
        {
            ("00000000-0000-0000-000B-000000000001", DefaultRoleDefinitions.Developer),
            ("00000000-0000-0000-000B-000000000002", DefaultRoleDefinitions.Tester),
            ("00000000-0000-0000-000B-000000000003", DefaultRoleDefinitions.ScrumMaster),
            ("00000000-0000-0000-000B-000000000004", DefaultRoleDefinitions.ProjectManager),
            ("00000000-0000-0000-000B-000000000005", DefaultRoleDefinitions.BusinessAnalyst),
        };

        foreach (var (id, value) in disciplines)
        {
            mb.Entity<RepositoryMetadata>().HasData(new
            {
                Id = new Guid(id),
                RepositoryId = (Guid?)DashRepoId,
                IsGlobal = false,
                Key = MetadataKey.RepoRole,
                Value = value,
                IsDeleted = false,
                DeletedAt = (DateTime?)null,
                DeletedByUserId = (Guid?)null,
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
            });
        }
    }

    // ── Demo repo's roles (default + custom) & members ────────────────────────
    private static void SeedRolesAndMembers(ModelBuilder mb)
    {
        // The demo repository's own default roles — repository-scoped, not editable.
        mb.Entity<Role>().HasData(
            new
            {
                Id = ScrumMasterRoleId,
                RepositoryId = (Guid?)DashRepoId,
                IsDefault = true,
                Name = "Scrum Master",
                Description = "Full access to all repository functions.",
                AllowedFunctions = new List<SystemFunction>(Enum.GetValues<SystemFunction>()),
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
            },
            new
            {
                Id = ProjectManagerRoleId,
                RepositoryId = (Guid?)DashRepoId,
                IsDefault = true,
                Name = "Project Manager",
                Description = "Manages sprints, capacity, and team settings in addition to all work-item and backlog operations.",
                AllowedFunctions = new List<SystemFunction>(Enum.GetValues<SystemFunction>()),
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
            },
            new
            {
                Id = DeveloperRoleId,
                RepositoryId = (Guid?)DashRepoId,
                IsDefault = true,
                Name = "Developer",
                Description = "Creates and edits sprint tasks, manages the backlog, and can promote items to sprints.",
                AllowedFunctions = new List<SystemFunction>
                {
                    SystemFunction.ViewRepository,  SystemFunction.CreateWorkItem,
                    SystemFunction.EditWorkItem,    SystemFunction.AssignWorkItem,
                    SystemFunction.ViewAnalytics,
                    SystemFunction.ManagePipeline,  SystemFunction.ManageRepo,
                },
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
            },
            new
            {
                Id = TesterRoleId,
                RepositoryId = (Guid?)DashRepoId,
                IsDefault = true,
                Name = "Tester",
                Description = "Creates and edits sprint tasks and backlog items; focused on quality and test coverage.",
                AllowedFunctions = new List<SystemFunction>
                {
                    SystemFunction.ViewRepository, SystemFunction.CreateWorkItem,
                    SystemFunction.EditWorkItem,   SystemFunction.AssignWorkItem,
                    SystemFunction.ManageBacklog,
                    SystemFunction.ViewAnalytics,
                },
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
            },
            new
            {
                Id = BusinessAnalystRoleId,
                RepositoryId = (Guid?)DashRepoId,
                IsDefault = true,
                Name = "Business Analyst",
                Description = "Manages the product backlog and requirements; read-only on sprint execution.",
                AllowedFunctions = new List<SystemFunction>
                {
                    SystemFunction.ViewRepository,  SystemFunction.CreateWorkItem,
                    SystemFunction.EditWorkItem,    SystemFunction.DeleteWorkItem,
                    SystemFunction.AssignWorkItem,  SystemFunction.ManageBacklog,
                    SystemFunction.PromoteToSprint, SystemFunction.ManageSprint,
                    SystemFunction.ActivateSprint,
                    SystemFunction.ViewAnalytics,
                },
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
            });

        // Example repository-scoped custom role.
        mb.Entity<Role>().HasData(new
        {
            Id = DevRoleId,
            RepositoryId = (Guid?)DashRepoId,
            IsDefault = false,
            Name = "Senior Developer",
            Description = "Full work-item and backlog access plus board and sprint management; no member/settings admin.",
            AllowedFunctions = new List<SystemFunction>
            {
                SystemFunction.ViewRepository, SystemFunction.CreateWorkItem,
                SystemFunction.EditWorkItem,   SystemFunction.AssignWorkItem,
                SystemFunction.ManageBacklog,  SystemFunction.PromoteToSprint,
                SystemFunction.ManageSprint,   SystemFunction.ManageBoard,
                SystemFunction.ViewAnalytics,
            },
            CreatedAt = SeedDate,
            UpdatedAt = (DateTime?)null,
        });

        mb.Entity<RepositoryMember>().HasData(
            new
            {
                Id = AdminMemberId,
                UserId = AdminUserId,
                RepositoryId = DashRepoId,
                RoleId = (Guid?)ScrumMasterRoleId,
                DefaultRole = "Scrum Master",
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
            },
            new
            {
                Id = DevMemberId,
                UserId = DevUserId,
                RepositoryId = DashRepoId,
                RoleId = (Guid?)DevRoleId,
                DefaultRole = "Developer",
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
            });
    }

    // ── Sprint Tasks (Bug + TestPlan standalone items) ────────────────────────
    private static void SeedItems(ModelBuilder mb)
    {
        // Seed Bug (standalone — not assigned to sprint yet)
        mb.Entity<SprintTask>().HasData(
            new
            {
                Id = Bug1Id,
                RepositoryId = DashRepoId,
                SprintId = (Guid?)null,
                BacklogItemId = (Guid?)null,
                ParentId = (Guid?)null,
                WorkItemNumber = 1,
                Type = SprintTaskType.Bug,
                Title = "Login page crashes on invalid credentials",
                Description = "Unhandled exception when submitting wrong password.\n\n**Actual:** NullReferenceException on blank page.\n**Expected:** Error message 'Invalid credentials' shown.",
                Priority = WorkItemPriority.High,
                AssignedToId = (Guid?)DevUserId,
                State = SprintTaskState.Active,
                StoryPoints = 0,
                OriginalEstimate = 0m,
                RemainingWork = 0m,
                CompletedWork = 0m,
                ClosedAt = (DateTime?)null,
                StepsToReproduce = (string?)"1. Open /login\n2. Enter wrong password\n3. Click Submit",
                Environment = (string?)"Chrome 124, Windows 11",
                RootCause = (string?)"Missing null-check on auth response before accessing token property.",
                Solution = (string?)string.Empty,
                Impaction = (string?)"All users unable to recover from login errors without refreshing.",
                UnitTest = (string?)"Add test: should display error message when credentials are invalid.",
                DesignReview = (string?)string.Empty,
                TestSteps = (List<string>?)null,
                Automated = (bool?)null,
                IsDeleted = false,
                DeletedAt = (DateTime?)null,
                DeletedByUserId = (Guid?)null,
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
            },
            new
            {
                Id = TestPlan1Id,
                RepositoryId = DashRepoId,
                SprintId = (Guid?)null,
                BacklogItemId = (Guid?)null,
                ParentId = (Guid?)null,
                WorkItemNumber = 2,
                Type = SprintTaskType.TestPlan,
                Title = "End-to-end login flow test plan",
                Description = "Validate the full authentication flow across Chrome, Firefox, and Edge.",
                Priority = WorkItemPriority.Medium,
                AssignedToId = (Guid?)AdminUserId,
                State = SprintTaskState.New,
                StoryPoints = 0,
                OriginalEstimate = 0m,
                RemainingWork = 0m,
                CompletedWork = 0m,
                ClosedAt = (DateTime?)null,
                StepsToReproduce = (string?)null,
                Environment = (string?)null,
                RootCause = (string?)null,
                Solution = (string?)null,
                Impaction = (string?)null,
                UnitTest = (string?)null,
                DesignReview = (string?)null,
                TestSteps = (List<string>?)new List<string>
                {
                    "Open /login in Chrome, Firefox, and Edge.",
                    "Enter valid credentials and submit.",
                    "Verify redirect to /overview and JWT stored in localStorage.",
                    "Enter invalid credentials and submit.",
                    "Verify error message is displayed without crash.",
                },
                Automated = (bool?)true,
                IsDeleted = false,
                DeletedAt = (DateTime?)null,
                DeletedByUserId = (Guid?)null,
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
            });
    }

    // ── Sprint ────────────────────────────────────────────────────────────────
    private static void SeedSprint(ModelBuilder mb)
    {
        mb.Entity<Sprint>().HasData(new
        {
            Id = Sprint1Id,
            RepositoryId = DashRepoId,
            Name = "Sprint 1 — May 2026",
            StartDate = new DateOnly(2026, 5, 4),
            EndDate = new DateOnly(2026, 5, 15),
            CreatedAt = SeedDate,
            UpdatedAt = (DateTime?)null,
        });

        mb.Entity<CapacityMember>().HasData(new
        {
            Id = new Guid("00000000-0000-0000-0009-000000000001"),
            SprintId = Sprint1Id,
            RepositoryId = DashRepoId,
            UserId = DevUserId,
            Role = "Developer",
            HoursPerDay = 8m,
            OvertimeHoursPerDay = 0m,
            CreatedAt = SeedDate,
            UpdatedAt = (DateTime?)null,
        });
    }

    // ── Backlog ───────────────────────────────────────────────────────────────
    private static void SeedBacklog(ModelBuilder mb)
    {
        mb.Entity<BacklogItem>().HasData(
            new
            {
                Id = BacklogEpic1Id,
                RepositoryId = DashRepoId,
                Type = BacklogItemType.Epic,
                Title = "Authentication & Authorization",
                ParentId = (Guid?)null,
                Rank = 1000m,
                Iteration = (string?)null,
                State = BacklogItemState.Refining,
                StoryPoints = (int?)null,
                TshirtSize = (TshirtSize?)TshirtSize.L,
                AcceptanceCriteria = string.Empty,
                Documents = new List<string>(),
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
            },
            new
            {
                Id = BacklogUs1Id,
                RepositoryId = DashRepoId,
                Type = BacklogItemType.UserStory,
                Title = "User can log in with email and password",
                ParentId = BacklogEpic1Id,
                Rank = 1000m,
                Iteration = "May 2026",
                State = BacklogItemState.Ready,
                StoryPoints = (int?)5,
                TshirtSize = (TshirtSize?)null,
                AcceptanceCriteria = "Given valid credentials, when I submit the login form, then I am redirected to /overview.",
                Documents = new List<string>(),
                CreatedAt = SeedDate,
                UpdatedAt = (DateTime?)null,
            });
    }

    // ── Smart Board ───────────────────────────────────────────────────────────
    private static void SeedSmartBoard(ModelBuilder mb)
    {
        var col1 = new Guid("00000000-0000-0000-000A-000000000001");
        var col2 = new Guid("00000000-0000-0000-000A-000000000002");
        var col3 = new Guid("00000000-0000-0000-000A-000000000003");
        var col4 = new Guid("00000000-0000-0000-000A-000000000004");

        mb.Entity<SmartBoardColumn>().HasData(
            new { Id = col1, RepositoryId = DashRepoId, Name = "New",       MappedState = SprintTaskState.New,      WipLimit = 0,  WipMode = WipMode.Soft, AgingLimitDays = 5,  Order = 0, CreatedAt = SeedDate, UpdatedAt = (DateTime?)null },
            new { Id = col2, RepositoryId = DashRepoId, Name = "Dev",       MappedState = SprintTaskState.Active,   WipLimit = 3,  WipMode = WipMode.Hard, AgingLimitDays = 3,  Order = 1, CreatedAt = SeedDate, UpdatedAt = (DateTime?)null },
            new { Id = col3, RepositoryId = DashRepoId, Name = "In Review", MappedState = SprintTaskState.InReview, WipLimit = 2,  WipMode = WipMode.Soft, AgingLimitDays = 2,  Order = 2, CreatedAt = SeedDate, UpdatedAt = (DateTime?)null },
            new { Id = col4, RepositoryId = DashRepoId, Name = "Done",      MappedState = SprintTaskState.Done,     WipLimit = 0,  WipMode = WipMode.Soft, AgingLimitDays = 30, Order = 3, CreatedAt = SeedDate, UpdatedAt = (DateTime?)null });
    }
}
