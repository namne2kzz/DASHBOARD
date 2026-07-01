using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DASHBOARD.Migrations
{
    /// <inheritdoc />
    public partial class MergeWorkItemIntoSprintTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionEntries_WorkItems_WorkItemId",
                table: "DiscussionEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_HistoryEntries_WorkItems_WorkItemId",
                table: "HistoryEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_SmartBoardCards_WorkItems_WorkItemId",
                table: "SmartBoardCards");

            migrationBuilder.DropTable(
                name: "WorkItems");

            migrationBuilder.RenameColumn(
                name: "WorkItemId",
                table: "SmartBoardCards",
                newName: "SprintTaskId");

            migrationBuilder.RenameIndex(
                name: "IX_SmartBoardCards_WorkItemId",
                table: "SmartBoardCards",
                newName: "IX_SmartBoardCards_SprintTaskId");

            migrationBuilder.RenameColumn(
                name: "WorkItemId",
                table: "HistoryEntries",
                newName: "SprintTaskId");

            migrationBuilder.RenameIndex(
                name: "IX_HistoryEntries_WorkItemId",
                table: "HistoryEntries",
                newName: "IX_HistoryEntries_SprintTaskId");

            migrationBuilder.RenameColumn(
                name: "WorkItemId",
                table: "DiscussionEntries",
                newName: "SprintTaskId");

            migrationBuilder.RenameIndex(
                name: "IX_DiscussionEntries_WorkItemId",
                table: "DiscussionEntries",
                newName: "IX_DiscussionEntries_SprintTaskId");

            migrationBuilder.AlterColumn<Guid>(
                name: "SprintId",
                table: "SprintTasks",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<bool>(
                name: "Automated",
                table: "SprintTasks",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "SprintTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "SprintTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "SprintTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SprintTasks",
                type: "nvarchar(max)",
                maxLength: 10000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DesignReview",
                table: "SprintTasks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Environment",
                table: "SprintTasks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Impaction",
                table: "SprintTasks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SprintTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "SprintTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RootCause",
                table: "SprintTasks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Solution",
                table: "SprintTasks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StepsToReproduce",
                table: "SprintTasks",
                type: "nvarchar(max)",
                maxLength: 5000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TestSteps",
                table: "SprintTasks",
                type: "nvarchar(max)",
                maxLength: 20000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitTest",
                table: "SprintTasks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkItemNumber",
                table: "SprintTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                table: "SprintTasks",
                columns: new[] { "Id", "AssignedToId", "Automated", "BacklogItemId", "ClosedAt", "CompletedWork", "CreatedAt", "DeletedAt", "DeletedByUserId", "Description", "DesignReview", "Environment", "Impaction", "IsDeleted", "OriginalEstimate", "ParentId", "Priority", "RemainingWork", "RepositoryId", "RootCause", "Solution", "SprintId", "State", "StepsToReproduce", "StoryPoints", "TestSteps", "Title", "Type", "UnitTest", "UpdatedAt", "WorkItemNumber" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0005-000000000002"), new Guid("00000000-0000-0000-0001-000000000002"), null, null, null, 0m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Unhandled exception when submitting wrong password.\n\n**Actual:** NullReferenceException on blank page.\n**Expected:** Error message 'Invalid credentials' shown.", "", "Chrome 124, Windows 11", "All users unable to recover from login errors without refreshing.", false, 0m, null, 2, 0m, new Guid("00000000-0000-0000-0002-000000000001"), "Missing null-check on auth response before accessing token property.", "", null, 3, "1. Open /login\n2. Enter wrong password\n3. Click Submit", 0, null, "Login page crashes on invalid credentials", 2, "Add test: should display error message when credentials are invalid.", null, 1 },
                    { new Guid("00000000-0000-0000-0005-000000000005"), new Guid("00000000-0000-0000-0001-000000000001"), true, null, null, 0m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Validate the full authentication flow across Chrome, Firefox, and Edge.", null, null, null, false, 0m, null, 1, 0m, new Guid("00000000-0000-0000-0002-000000000001"), null, null, null, 0, null, 0, "[\"Open /login in Chrome, Firefox, and Edge.\",\"Enter valid credentials and submit.\",\"Verify redirect to /overview and JWT stored in localStorage.\",\"Enter invalid credentials and submit.\",\"Verify error message is displayed without crash.\"]", "End-to-end login flow test plan", 3, null, null, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SprintTasks_RepositoryId_Type_State",
                table: "SprintTasks",
                columns: new[] { "RepositoryId", "Type", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_SprintTasks_RepositoryId_WorkItemNumber",
                table: "SprintTasks",
                columns: new[] { "RepositoryId", "WorkItemNumber" },
                unique: true);

            // Clear orphaned rows whose SprintTaskId no longer exists in SprintTasks
            // (previously pointed at WorkItems which have been dropped).
            migrationBuilder.Sql(
                "DELETE FROM DiscussionEntries WHERE SprintTaskId NOT IN (SELECT Id FROM SprintTasks)");
            migrationBuilder.Sql(
                "DELETE FROM HistoryEntries WHERE SprintTaskId NOT IN (SELECT Id FROM SprintTasks)");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionEntries_SprintTasks_SprintTaskId",
                table: "DiscussionEntries",
                column: "SprintTaskId",
                principalTable: "SprintTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HistoryEntries_SprintTasks_SprintTaskId",
                table: "HistoryEntries",
                column: "SprintTaskId",
                principalTable: "SprintTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SmartBoardCards_SprintTasks_SprintTaskId",
                table: "SmartBoardCards",
                column: "SprintTaskId",
                principalTable: "SprintTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionEntries_SprintTasks_SprintTaskId",
                table: "DiscussionEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_HistoryEntries_SprintTasks_SprintTaskId",
                table: "HistoryEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_SmartBoardCards_SprintTasks_SprintTaskId",
                table: "SmartBoardCards");

            migrationBuilder.DropIndex(
                name: "IX_SprintTasks_RepositoryId_Type_State",
                table: "SprintTasks");

            migrationBuilder.DropIndex(
                name: "IX_SprintTasks_RepositoryId_WorkItemNumber",
                table: "SprintTasks");

            migrationBuilder.DeleteData(
                table: "SprintTasks",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000002"));

            migrationBuilder.DeleteData(
                table: "SprintTasks",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000005"));

            migrationBuilder.DropColumn(
                name: "Automated",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "DesignReview",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "Environment",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "Impaction",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "RootCause",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "Solution",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "StepsToReproduce",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "TestSteps",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "UnitTest",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "WorkItemNumber",
                table: "SprintTasks");

            migrationBuilder.RenameColumn(
                name: "SprintTaskId",
                table: "SmartBoardCards",
                newName: "WorkItemId");

            migrationBuilder.RenameIndex(
                name: "IX_SmartBoardCards_SprintTaskId",
                table: "SmartBoardCards",
                newName: "IX_SmartBoardCards_WorkItemId");

            migrationBuilder.RenameColumn(
                name: "SprintTaskId",
                table: "HistoryEntries",
                newName: "WorkItemId");

            migrationBuilder.RenameIndex(
                name: "IX_HistoryEntries_SprintTaskId",
                table: "HistoryEntries",
                newName: "IX_HistoryEntries_WorkItemId");

            migrationBuilder.RenameColumn(
                name: "SprintTaskId",
                table: "DiscussionEntries",
                newName: "WorkItemId");

            migrationBuilder.RenameIndex(
                name: "IX_DiscussionEntries_SprintTaskId",
                table: "DiscussionEntries",
                newName: "IX_DiscussionEntries_WorkItemId");

            migrationBuilder.AlterColumn<Guid>(
                name: "SprintId",
                table: "SprintTasks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "WorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptanceCriteria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Automated = table.Column<bool>(type: "bit", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedWork = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DesignReview = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Environment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FixedInVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Impaction = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImplementInBuild = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Links = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalEstimate = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RemainingWork = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    RootCause = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Solution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sprint = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "New"),
                    StepsToReproduce = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StoryPoints = table.Column<int>(type: "int", nullable: true),
                    TestSteps = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TestSuiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UnitTest = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkItemNumber = table.Column<int>(type: "int", nullable: false),
                    WorkItemType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
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

            migrationBuilder.InsertData(
                table: "WorkItems",
                columns: new[] { "Id", "AcceptanceCriteria", "AssignedToId", "Automated", "ClosedAt", "CompletedWork", "CreatedAt", "CustomFieldsJson", "DeletedAt", "DeletedByUserId", "Description", "DesignReview", "Environment", "FixedInVersion", "Impaction", "ImplementInBuild", "Links", "OriginalEstimate", "Priority", "RemainingWork", "RepositoryId", "RootCause", "Solution", "Sprint", "State", "StepsToReproduce", "StoryPoints", "TestSteps", "TestSuiteId", "Title", "UnitTest", "UpdatedAt", "WorkItemNumber", "WorkItemType" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0005-000000000001"), "Given valid credentials, when I log in, then I receive a JWT and am redirected to /overview.", new Guid("00000000-0000-0000-0001-000000000002"), null, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "{}", null, null, "Implement JWT-based login flow with token storage.", null, null, null, null, null, "[]", null, "High", null, new Guid("00000000-0000-0000-0002-000000000001"), null, null, "May 2026", "New", null, 5, null, null, "As a user, I want to log in with my credentials", null, null, 1, "UserStory" },
                    { new Guid("00000000-0000-0000-0005-000000000002"), null, new Guid("00000000-0000-0000-0001-000000000002"), null, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "{}", null, null, "Unhandled exception when submitting wrong password.\n\n**Actual:** Application throws NullReferenceException and shows blank page.\n**Expected:** Error message 'Invalid credentials' displayed below the form.", "", "Chrome 124, Windows 11", null, "All users unable to recover from login errors without refreshing the page.", null, "[]", null, "High", null, new Guid("00000000-0000-0000-0002-000000000001"), "Missing null-check on auth response before accessing token property.", "", "May 2026", "Active", "1. Open /login\n2. Enter wrong password\n3. Click Submit", null, null, null, "Login page crashes on invalid credentials", "Add test: should display error message when credentials are invalid.", null, 2, "Bug" },
                    { new Guid("00000000-0000-0000-0005-000000000003"), null, new Guid("00000000-0000-0000-0001-000000000002"), null, null, 1.5m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "{}", null, null, "Cover LoginAsync, RefreshTokenAsync, and RevokeTokenAsync with xUnit + Moq.", "", null, null, null, null, "[]", 4m, "Medium", 2.5m, new Guid("00000000-0000-0000-0002-000000000001"), null, null, "May 2026", "Active", null, null, null, null, "Write unit tests for AuthService", "Cover LoginAsync, RefreshTokenAsync, RevokeTokenAsync — aim for 100% branch coverage.", null, 3, "Task" },
                    { new Guid("00000000-0000-0000-0005-000000000004"), null, null, null, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "{}", null, null, "Show an in-app notification 5 minutes before the session expires so users can renew without losing their work.", null, null, null, null, null, "[]", null, "Low", null, new Guid("00000000-0000-0000-0002-000000000001"), null, null, "May 2026", "New", null, null, null, null, "Improve JWT refresh token expiry UX", null, null, 4, "Improvement" },
                    { new Guid("00000000-0000-0000-0005-000000000005"), null, new Guid("00000000-0000-0000-0001-000000000001"), true, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "{}", null, null, "Validate the full authentication flow across major browsers: Chrome, Firefox, Edge.", null, null, null, null, null, "[]", null, "Medium", null, new Guid("00000000-0000-0000-0002-000000000001"), null, null, "May 2026", "Design", null, null, "[\"Open /login in Chrome, Firefox, and Edge.\",\"Enter valid credentials and submit.\",\"Verify redirect to /overview and JWT stored in localStorage.\",\"Enter invalid credentials and submit.\",\"Verify error message is displayed without crash.\",\"Let session expire; verify refresh token renews silently.\"]", null, "End-to-end login flow test plan", null, null, 5, "TestPlan" }
                });

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

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionEntries_WorkItems_WorkItemId",
                table: "DiscussionEntries",
                column: "WorkItemId",
                principalTable: "WorkItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HistoryEntries_WorkItems_WorkItemId",
                table: "HistoryEntries",
                column: "WorkItemId",
                principalTable: "WorkItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SmartBoardCards_WorkItems_WorkItemId",
                table: "SmartBoardCards",
                column: "WorkItemId",
                principalTable: "WorkItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
