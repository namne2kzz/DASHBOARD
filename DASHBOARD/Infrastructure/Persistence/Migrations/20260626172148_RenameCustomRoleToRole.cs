using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameCustomRoleToRole : Migration
    {
        // Global default role GUIDs (kept in sync with SeedData).
        private const string ScrumMasterRoleId    = "00000000-0000-0000-0003-000000000010";
        private const string ProjectManagerRoleId = "00000000-0000-0000-0003-000000000011";
        private const string DeveloperRoleId       = "00000000-0000-0000-0003-000000000012";
        private const string TesterRoleId          = "00000000-0000-0000-0003-000000000013";
        private const string BusinessAnalystRoleId = "00000000-0000-0000-0003-000000000014";
        private const string SeniorDeveloperRoleId = "00000000-0000-0000-0003-000000000001"; // existing custom seed role

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Rename the table (preserves all existing custom-role rows) ──────────
            migrationBuilder.DropForeignKey(
                name: "FK_RepositoryMembers_CustomRoles_CustomRoleId",
                table: "RepositoryMembers");

            migrationBuilder.RenameTable(
                name: "CustomRoles",
                newName: "Roles");

            // Rename the carried-over constraints/index to match the new table name.
            migrationBuilder.Sql("EXEC sp_rename N'PK_CustomRoles', N'PK_Roles';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_CustomRoles_Repositories_RepositoryId', N'FK_Roles_Repositories_RepositoryId';");
            migrationBuilder.Sql("EXEC sp_rename N'Roles.IX_CustomRoles_RepositoryId', N'IX_Roles_RepositoryId', N'INDEX';");

            // ── 2. New columns / nullability on Roles ──────────────────────────────────
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "RepositoryId",
                table: "Roles",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            // ── 3. Rename the member FK column ─────────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "CustomRoleId",
                table: "RepositoryMembers",
                newName: "RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_RepositoryMembers_CustomRoleId",
                table: "RepositoryMembers",
                newName: "IX_RepositoryMembers_RoleId");

            // ── 4. Convert the existing "Developer" custom seed role to "Senior Developer" ─
            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid(SeniorDeveloperRoleId),
                column: "Name",
                value: "Senior Developer");

            // ── 5. Seed the global default roles ───────────────────────────────────────
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "AllowedFunctions", "CreatedAt", "Description", "IsDefault", "Name", "RepositoryId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid(ScrumMasterRoleId),    "[0,1,2,3,4,5,6,7,8,9]", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Full access to all repository functions.", true, "Scrum Master", null, null },
                    { new Guid(ProjectManagerRoleId), "[0,3,4,6,7,2]",         new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Manages sprints, capacity, and settings in addition to work items.", true, "Project Manager", null, null },
                    { new Guid(DeveloperRoleId),      "[0,3,4]",               new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Create and edit work items.", true, "Developer", null, null },
                    { new Guid(TesterRoleId),         "[0,3,4]",               new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Create and edit work items.", true, "Tester", null, null },
                    { new Guid(BusinessAnalystRoleId),"[0,3,4]",               new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Create and edit work items.", true, "Business Analyst", null, null }
                });

            // ── 6. Link existing members with no role to the default role matching their
            //        current team role (DefaultRole enum: Dev=0, Tester=1, ScrumMaster=2, ProjectManager=3). ──
            migrationBuilder.Sql($"UPDATE [RepositoryMembers] SET [RoleId] = '{DeveloperRoleId}'       WHERE [RoleId] IS NULL AND [DefaultRole] = 0;");
            migrationBuilder.Sql($"UPDATE [RepositoryMembers] SET [RoleId] = '{TesterRoleId}'          WHERE [RoleId] IS NULL AND [DefaultRole] = 1;");
            migrationBuilder.Sql($"UPDATE [RepositoryMembers] SET [RoleId] = '{ScrumMasterRoleId}'     WHERE [RoleId] IS NULL AND [DefaultRole] = 2;");
            migrationBuilder.Sql($"UPDATE [RepositoryMembers] SET [RoleId] = '{ProjectManagerRoleId}'  WHERE [RoleId] IS NULL AND [DefaultRole] = 3;");

            // ── 7. Re-create the member → role FK ──────────────────────────────────────
            migrationBuilder.AddForeignKey(
                name: "FK_RepositoryMembers_Roles_RoleId",
                table: "RepositoryMembers",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RepositoryMembers_Roles_RoleId",
                table: "RepositoryMembers");

            // Unlink members that point at default roles, then remove the default roles.
            migrationBuilder.Sql(
                "UPDATE [RepositoryMembers] SET [RoleId] = NULL WHERE [RoleId] IN (" +
                $"'{ScrumMasterRoleId}','{ProjectManagerRoleId}','{DeveloperRoleId}','{TesterRoleId}','{BusinessAnalystRoleId}');");

            migrationBuilder.DeleteData(table: "Roles", keyColumn: "Id", keyValue: new Guid(ScrumMasterRoleId));
            migrationBuilder.DeleteData(table: "Roles", keyColumn: "Id", keyValue: new Guid(ProjectManagerRoleId));
            migrationBuilder.DeleteData(table: "Roles", keyColumn: "Id", keyValue: new Guid(DeveloperRoleId));
            migrationBuilder.DeleteData(table: "Roles", keyColumn: "Id", keyValue: new Guid(TesterRoleId));
            migrationBuilder.DeleteData(table: "Roles", keyColumn: "Id", keyValue: new Guid(BusinessAnalystRoleId));

            // Restore the renamed custom seed role.
            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid(SeniorDeveloperRoleId),
                column: "Name",
                value: "Developer");

            migrationBuilder.RenameColumn(
                name: "RoleId",
                table: "RepositoryMembers",
                newName: "CustomRoleId");

            migrationBuilder.RenameIndex(
                name: "IX_RepositoryMembers_RoleId",
                table: "RepositoryMembers",
                newName: "IX_RepositoryMembers_CustomRoleId");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Roles");

            migrationBuilder.AlterColumn<Guid>(
                name: "RepositoryId",
                table: "Roles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.Sql("EXEC sp_rename N'Roles.IX_Roles_RepositoryId', N'IX_CustomRoles_RepositoryId', N'INDEX';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_Roles_Repositories_RepositoryId', N'FK_CustomRoles_Repositories_RepositoryId';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_Roles', N'PK_CustomRoles';");

            migrationBuilder.RenameTable(
                name: "Roles",
                newName: "CustomRoles");

            migrationBuilder.AddForeignKey(
                name: "FK_RepositoryMembers_CustomRoles_CustomRoleId",
                table: "RepositoryMembers",
                column: "CustomRoleId",
                principalTable: "CustomRoles",
                principalColumn: "Id");
        }
    }
}
