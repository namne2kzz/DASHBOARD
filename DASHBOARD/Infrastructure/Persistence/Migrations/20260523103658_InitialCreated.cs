using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BacklogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Rank = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Iteration = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    StoryPoints = table.Column<int>(type: "int", nullable: true),
                    TshirtSize = table.Column<int>(type: "int", nullable: true),
                    AcceptanceCriteria = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Documents = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacklogItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BacklogItems_BacklogItems_ParentId",
                        column: x => x.ParentId,
                        principalTable: "BacklogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmartBoardColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MappedState = table.Column<int>(type: "int", nullable: false),
                    WipLimit = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    WipMode = table.Column<int>(type: "int", nullable: false),
                    Split = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AgingLimitDays = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartBoardColumns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sprints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PasswordSalt = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AvatarClass = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "bg-slate-600"),
                    IsGlobalAdmin = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AuthProvider = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    GoogleSubjectId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Users_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WikiPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WikiPages_WikiPages_ParentId",
                        column: x => x.ParentId,
                        principalTable: "WikiPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AllowedFunctions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomRoles_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryMetadata_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapacityMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    HoursPerDay = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    OvertimeHoursPerDay = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapacityMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CapacityMembers_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapacityMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DaysOff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Hours = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DaysOff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DaysOff_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DaysOff_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SprintTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BacklogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    StoryPoints = table.Column<int>(type: "int", nullable: false),
                    OriginalEstimate = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    RemainingWork = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    CompletedWork = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintTasks_BacklogItems_BacklogItemId",
                        column: x => x.BacklogItemId,
                        principalTable: "BacklogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SprintTasks_SprintTasks_ParentId",
                        column: x => x.ParentId,
                        principalTable: "SprintTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SprintTasks_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SprintTasks_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JwtId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefreshTokenHash = table.Column<string>(type: "nvarchar(88)", maxLength: 88, nullable: false),
                    RefreshTokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RefreshTokenIsRevoked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RefreshTokenRevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "New"),
                    WorkItemType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Sprint = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImplementInBuild = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FixedInVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Links = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptanceCriteria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StoryPoints = table.Column<int>(type: "int", nullable: true),
                    StepsToReproduce = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Environment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RootCause = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Solution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Impaction = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UnitTest = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DesignReview = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalEstimate = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    RemainingWork = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    CompletedWork = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    TestSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Automated = table.Column<bool>(type: "bit", nullable: true),
                    TestSteps = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItems_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItems_Users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultRole = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryMembers_CustomRoles_CustomRoleId",
                        column: x => x.CustomRoleId,
                        principalTable: "CustomRoles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RepositoryMembers_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscussionEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscussionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscussionEntries_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiscussionEntries_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryEntries_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoryEntries_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmartBoardCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColumnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    SwimlaneId = table.Column<int>(type: "int", nullable: false),
                    SplitSide = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnteredColumnAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartBoardCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmartBoardCards_SmartBoardColumns_ColumnId",
                        column: x => x.ColumnId,
                        principalTable: "SmartBoardColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmartBoardCards_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "BacklogItems",
                columns: new[] { "Id", "AcceptanceCriteria", "CreatedAt", "Documents", "Iteration", "ParentId", "Rank", "RepositoryId", "State", "StoryPoints", "Title", "TshirtSize", "Type", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0008-000000000001"), "", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "[]", null, null, 1000m, new Guid("00000000-0000-0000-0002-000000000001"), 1, null, "Authentication & Authorization", 3, 0, null });

            migrationBuilder.InsertData(
                table: "Repositories",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "Name", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0002-000000000001"), "DASH", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Main project repository for the DASHBOARD application.", "Dashboard Project", null });

            migrationBuilder.InsertData(
                table: "SmartBoardColumns",
                columns: new[] { "Id", "AgingLimitDays", "CreatedAt", "MappedState", "Name", "Order", "RepositoryId", "UpdatedAt", "WipMode" },
                values: new object[] { new Guid("00000000-0000-0000-000a-000000000001"), 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0, "New", 0, new Guid("00000000-0000-0000-0002-000000000001"), null, 0 });

            migrationBuilder.InsertData(
                table: "SmartBoardColumns",
                columns: new[] { "Id", "AgingLimitDays", "CreatedAt", "MappedState", "Name", "Order", "RepositoryId", "Split", "UpdatedAt", "WipLimit", "WipMode" },
                values: new object[] { new Guid("00000000-0000-0000-000a-000000000002"), 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, "Dev", 1, new Guid("00000000-0000-0000-0002-000000000001"), true, null, 3, 1 });

            migrationBuilder.InsertData(
                table: "SmartBoardColumns",
                columns: new[] { "Id", "AgingLimitDays", "CreatedAt", "MappedState", "Name", "Order", "RepositoryId", "UpdatedAt", "WipLimit", "WipMode" },
                values: new object[] { new Guid("00000000-0000-0000-000a-000000000003"), 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, "In Review", 2, new Guid("00000000-0000-0000-0002-000000000001"), null, 2, 0 });

            migrationBuilder.InsertData(
                table: "SmartBoardColumns",
                columns: new[] { "Id", "AgingLimitDays", "CreatedAt", "MappedState", "Name", "Order", "RepositoryId", "UpdatedAt", "WipMode" },
                values: new object[] { new Guid("00000000-0000-0000-000a-000000000004"), 30, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, "Done", 3, new Guid("00000000-0000-0000-0002-000000000001"), null, 0 });

            migrationBuilder.InsertData(
                table: "Sprints",
                columns: new[] { "Id", "CreatedAt", "EndDate", "IsActive", "Name", "RepositoryId", "StartDate", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0007-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 5, 15), true, "Sprint 1 — May 2026", new Guid("00000000-0000-0000-0002-000000000001"), new DateOnly(2026, 5, 4), null });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarClass", "CreatedAt", "DeletedAt", "DeletedByUserId", "Email", "GoogleSubjectId", "IsGlobalAdmin", "ManagerId", "Name", "PasswordHash", "PasswordSalt", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0001-000000000001"), "bg-sky-600", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "admin@dashboard.local", null, true, null, "Admin User", "X7gKv4CsinoBoFpwmHYf1N6UWn3XsQZoB0KFQ5fzmkMTANRcm7kbuThFVIEQpx1xb5eT1dGbhTKtWoBpzr0U6A==", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", null });

            migrationBuilder.InsertData(
                table: "WikiPages",
                columns: new[] { "Id", "Content", "CreatedAt", "DeletedAt", "DeletedByUserId", "LastUpdated", "ParentId", "RepositoryId", "Title", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0006-000000000001"), "<h1>Architecture</h1><p>Full-stack .NET 10 + Angular v19 application.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0002-000000000001"), "System Architecture Overview", null });

            migrationBuilder.InsertData(
                table: "BacklogItems",
                columns: new[] { "Id", "AcceptanceCriteria", "CreatedAt", "Documents", "Iteration", "ParentId", "Rank", "RepositoryId", "State", "StoryPoints", "Title", "TshirtSize", "Type", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0008-000000000002"), "Given valid credentials, when I submit the login form, then I am redirected to /overview.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "[]", "May 2026", new Guid("00000000-0000-0000-0008-000000000001"), 1000m, new Guid("00000000-0000-0000-0002-000000000001"), 2, 5, "User can log in with email and password", null, 2, null });

            migrationBuilder.InsertData(
                table: "CustomRoles",
                columns: new[] { "Id", "AllowedFunctions", "CreatedAt", "Description", "Name", "RepositoryId", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0003-000000000001"), "[0,3,4,9,6]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Full access to work items and board; read-only on settings.", "Developer", new Guid("00000000-0000-0000-0002-000000000001"), null });

            migrationBuilder.InsertData(
                table: "RepositoryMembers",
                columns: new[] { "Id", "CreatedAt", "CustomRoleId", "DefaultRole", "RepositoryId", "UpdatedAt", "UserId" },
                values: new object[] { new Guid("00000000-0000-0000-0004-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, new Guid("00000000-0000-0000-0002-000000000001"), null, new Guid("00000000-0000-0000-0001-000000000001") });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarClass", "CreatedAt", "DeletedAt", "DeletedByUserId", "Email", "GoogleSubjectId", "ManagerId", "Name", "PasswordHash", "PasswordSalt", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0001-000000000002"), "bg-violet-600", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "dev@dashboard.local", null, new Guid("00000000-0000-0000-0001-000000000001"), "Dev User", "BF511GUnt57DQ+nwLLCRiKYHFj5nSCXA9RCUwUrV4pDG1WFwJwNMLFM8skh8SafmuxhaXuIgkRNqQjZd7ey6/w==", "AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQE=", null });

            migrationBuilder.InsertData(
                table: "WikiPages",
                columns: new[] { "Id", "Content", "CreatedAt", "DeletedAt", "DeletedByUserId", "LastUpdated", "ParentId", "RepositoryId", "Title", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0006-000000000002"), "<h2>Auth</h2><p>JWT Bearer tokens with PBKDF2-SHA512 password hashing.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0006-000000000001"), new Guid("00000000-0000-0000-0002-000000000001"), "Authentication Flow", null });

            migrationBuilder.InsertData(
                table: "WorkItems",
                columns: new[] { "Id", "AcceptanceCriteria", "AssignedToId", "Automated", "ClosedAt", "CompletedWork", "CreatedAt", "CustomFieldsJson", "DeletedAt", "DeletedByUserId", "Description", "DesignReview", "Environment", "FixedInVersion", "Impaction", "ImplementInBuild", "Links", "OriginalEstimate", "Priority", "RemainingWork", "RepositoryId", "RootCause", "Solution", "Sprint", "StepsToReproduce", "StoryPoints", "TestSteps", "TestSuiteId", "Title", "UnitTest", "UpdatedAt", "WorkItemNumber", "WorkItemType" },
                values: new object[] { new Guid("00000000-0000-0000-0005-000000000004"), null, null, null, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "{}", null, null, "Show an in-app notification 5 minutes before the session expires so users can renew without losing their work.", null, null, null, null, null, "[]", null, "Low", null, new Guid("00000000-0000-0000-0002-000000000001"), null, null, "May 2026", null, null, null, null, "Improve JWT refresh token expiry UX", null, null, 4, "Improvement" });

            migrationBuilder.InsertData(
                table: "WorkItems",
                columns: new[] { "Id", "AcceptanceCriteria", "AssignedToId", "Automated", "ClosedAt", "CompletedWork", "CreatedAt", "CustomFieldsJson", "DeletedAt", "DeletedByUserId", "Description", "DesignReview", "Environment", "FixedInVersion", "Impaction", "ImplementInBuild", "Links", "OriginalEstimate", "Priority", "RemainingWork", "RepositoryId", "RootCause", "Solution", "Sprint", "State", "StepsToReproduce", "StoryPoints", "TestSteps", "TestSuiteId", "Title", "UnitTest", "UpdatedAt", "WorkItemNumber", "WorkItemType" },
                values: new object[] { new Guid("00000000-0000-0000-0005-000000000005"), null, new Guid("00000000-0000-0000-0001-000000000001"), true, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "{}", null, null, "Validate the full authentication flow across major browsers: Chrome, Firefox, Edge.", null, null, null, null, null, "[]", null, "Medium", null, new Guid("00000000-0000-0000-0002-000000000001"), null, null, "May 2026", "Design", null, null, "[\"Open /login in Chrome, Firefox, and Edge.\",\"Enter valid credentials and submit.\",\"Verify redirect to /overview and JWT stored in localStorage.\",\"Enter invalid credentials and submit.\",\"Verify error message is displayed without crash.\",\"Let session expire; verify refresh token renews silently.\"]", null, "End-to-end login flow test plan", null, null, 5, "TestPlan" });

            migrationBuilder.InsertData(
                table: "CapacityMembers",
                columns: new[] { "Id", "CreatedAt", "HoursPerDay", "OvertimeHoursPerDay", "RepositoryId", "Role", "SprintId", "UpdatedAt", "UserId" },
                values: new object[] { new Guid("00000000-0000-0000-0009-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 8m, 0m, new Guid("00000000-0000-0000-0002-000000000001"), 0, new Guid("00000000-0000-0000-0007-000000000001"), null, new Guid("00000000-0000-0000-0001-000000000002") });

            migrationBuilder.InsertData(
                table: "RepositoryMembers",
                columns: new[] { "Id", "CreatedAt", "CustomRoleId", "DefaultRole", "RepositoryId", "UpdatedAt", "UserId" },
                values: new object[] { new Guid("00000000-0000-0000-0004-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0003-000000000001"), 0, new Guid("00000000-0000-0000-0002-000000000001"), null, new Guid("00000000-0000-0000-0001-000000000002") });

            migrationBuilder.InsertData(
                table: "WorkItems",
                columns: new[] { "Id", "AcceptanceCriteria", "AssignedToId", "Automated", "ClosedAt", "CompletedWork", "CreatedAt", "CustomFieldsJson", "DeletedAt", "DeletedByUserId", "Description", "DesignReview", "Environment", "FixedInVersion", "Impaction", "ImplementInBuild", "Links", "OriginalEstimate", "Priority", "RemainingWork", "RepositoryId", "RootCause", "Solution", "Sprint", "StepsToReproduce", "StoryPoints", "TestSteps", "TestSuiteId", "Title", "UnitTest", "UpdatedAt", "WorkItemNumber", "WorkItemType" },
                values: new object[] { new Guid("00000000-0000-0000-0005-000000000001"), "Given valid credentials, when I log in, then I receive a JWT and am redirected to /overview.", new Guid("00000000-0000-0000-0001-000000000002"), null, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "{}", null, null, "Implement JWT-based login flow with token storage.", null, null, null, null, null, "[]", null, "High", null, new Guid("00000000-0000-0000-0002-000000000001"), null, null, "May 2026", null, 5, null, null, "As a user, I want to log in with my credentials", null, null, 1, "UserStory" });

            migrationBuilder.InsertData(
                table: "WorkItems",
                columns: new[] { "Id", "AcceptanceCriteria", "AssignedToId", "Automated", "ClosedAt", "CompletedWork", "CreatedAt", "CustomFieldsJson", "DeletedAt", "DeletedByUserId", "Description", "DesignReview", "Environment", "FixedInVersion", "Impaction", "ImplementInBuild", "Links", "OriginalEstimate", "Priority", "RemainingWork", "RepositoryId", "RootCause", "Solution", "Sprint", "State", "StepsToReproduce", "StoryPoints", "TestSteps", "TestSuiteId", "Title", "UnitTest", "UpdatedAt", "WorkItemNumber", "WorkItemType" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0005-000000000002"), null, new Guid("00000000-0000-0000-0001-000000000002"), null, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "{}", null, null, "Unhandled exception when submitting wrong password.\n\n**Actual:** Application throws NullReferenceException and shows blank page.\n**Expected:** Error message 'Invalid credentials' displayed below the form.", "", "Chrome 124, Windows 11", null, "All users unable to recover from login errors without refreshing the page.", null, "[]", null, "High", null, new Guid("00000000-0000-0000-0002-000000000001"), "Missing null-check on auth response before accessing token property.", "", "May 2026", "Active", "1. Open /login\n2. Enter wrong password\n3. Click Submit", null, null, null, "Login page crashes on invalid credentials", "Add test: should display error message when credentials are invalid.", null, 2, "Bug" },
                    { new Guid("00000000-0000-0000-0005-000000000003"), null, new Guid("00000000-0000-0000-0001-000000000002"), null, null, 1.5m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "{}", null, null, "Cover LoginAsync, RefreshTokenAsync, and RevokeTokenAsync with xUnit + Moq.", "", null, null, null, null, "[]", 4m, "Medium", 2.5m, new Guid("00000000-0000-0000-0002-000000000001"), null, null, "May 2026", "Active", null, null, null, null, "Write unit tests for AuthService", "Cover LoginAsync, RefreshTokenAsync, RevokeTokenAsync — aim for 100% branch coverage.", null, 3, "Task" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BacklogItems_ParentId",
                table: "BacklogItems",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_BacklogItems_RepositoryId",
                table: "BacklogItems",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BacklogItems_RepositoryId_Type_Rank",
                table: "BacklogItems",
                columns: new[] { "RepositoryId", "Type", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_CapacityMembers_RepositoryId",
                table: "CapacityMembers",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CapacityMembers_SprintId_UserId",
                table: "CapacityMembers",
                columns: new[] { "SprintId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CapacityMembers_UserId",
                table: "CapacityMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomRoles_RepositoryId",
                table: "CustomRoles",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DaysOff_RepositoryId",
                table: "DaysOff",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DaysOff_SprintId",
                table: "DaysOff",
                column: "SprintId");

            migrationBuilder.CreateIndex(
                name: "IX_DaysOff_UserId",
                table: "DaysOff",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionEntries_AuthorId",
                table: "DiscussionEntries",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionEntries_RepositoryId",
                table: "DiscussionEntries",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionEntries_WorkItemId",
                table: "DiscussionEntries",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEntries_AuthorId",
                table: "HistoryEntries",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEntries_RepositoryId",
                table: "HistoryEntries",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEntries_WorkItemId",
                table: "HistoryEntries",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Email_RepositoryId_Status",
                table: "Invitations",
                columns: new[] { "Email", "RepositoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InvitedByUserId",
                table: "Invitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_RepositoryId",
                table: "Invitations",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TokenHash",
                table: "Invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Code",
                table: "Repositories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_IsArchived",
                table: "Repositories",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryMembers_CustomRoleId",
                table: "RepositoryMembers",
                column: "CustomRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryMembers_RepositoryId",
                table: "RepositoryMembers",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryMembers_UserId_RepositoryId",
                table: "RepositoryMembers",
                columns: new[] { "UserId", "RepositoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryMetadata_IsDeleted",
                table: "RepositoryMetadata",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryMetadata_RepoId_Key",
                table: "RepositoryMetadata",
                columns: new[] { "RepositoryId", "Key" });

            migrationBuilder.CreateIndex(
                name: "UX_RepositoryMetadata_RepoId_Key_Value_Active",
                table: "RepositoryMetadata",
                columns: new[] { "RepositoryId", "Key", "Value" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SmartBoardCards_ColumnId",
                table: "SmartBoardCards",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartBoardCards_RepositoryId",
                table: "SmartBoardCards",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartBoardCards_WorkItemId",
                table: "SmartBoardCards",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartBoardColumns_RepositoryId_Order",
                table: "SmartBoardColumns",
                columns: new[] { "RepositoryId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_RepositoryId",
                table: "Sprints",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_RepositoryId_IsActive",
                table: "Sprints",
                columns: new[] { "RepositoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SprintTasks_AssignedToId",
                table: "SprintTasks",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintTasks_BacklogItemId",
                table: "SprintTasks",
                column: "BacklogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintTasks_ParentId",
                table: "SprintTasks",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintTasks_RepositoryId",
                table: "SprintTasks",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintTasks_SprintId",
                table: "SprintTasks",
                column: "SprintId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleSubjectId",
                table: "Users",
                column: "GoogleSubjectId",
                unique: true,
                filter: "[GoogleSubjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsDeleted",
                table: "Users",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagerId",
                table: "Users",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_AccessTokenExpiresAt",
                table: "UserTokens",
                column: "AccessTokenExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_JwtId",
                table: "UserTokens",
                column: "JwtId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_RefreshTokenExpiresAt",
                table: "UserTokens",
                column: "RefreshTokenExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_RefreshTokenHash",
                table: "UserTokens",
                column: "RefreshTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_UserId_JwtId",
                table: "UserTokens",
                columns: new[] { "UserId", "JwtId" });

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_IsDeleted",
                table: "WikiPages",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_ParentId",
                table: "WikiPages",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_RepositoryId",
                table: "WikiPages",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_RepositoryId_ParentId",
                table: "WikiPages",
                columns: new[] { "RepositoryId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_AssignedToId",
                table: "WorkItems",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_IsDeleted",
                table: "WorkItems",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_RepositoryId_WorkItemNumber",
                table: "WorkItems",
                columns: new[] { "RepositoryId", "WorkItemNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapacityMembers");

            migrationBuilder.DropTable(
                name: "DaysOff");

            migrationBuilder.DropTable(
                name: "DiscussionEntries");

            migrationBuilder.DropTable(
                name: "HistoryEntries");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "RepositoryMembers");

            migrationBuilder.DropTable(
                name: "RepositoryMetadata");

            migrationBuilder.DropTable(
                name: "SmartBoardCards");

            migrationBuilder.DropTable(
                name: "SprintTasks");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "WikiPages");

            migrationBuilder.DropTable(
                name: "CustomRoles");

            migrationBuilder.DropTable(
                name: "SmartBoardColumns");

            migrationBuilder.DropTable(
                name: "WorkItems");

            migrationBuilder.DropTable(
                name: "BacklogItems");

            migrationBuilder.DropTable(
                name: "Sprints");

            migrationBuilder.DropTable(
                name: "Repositories");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
