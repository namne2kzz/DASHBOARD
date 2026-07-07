using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000001"),
                columns: new[] { "AllowedFunctions", "Description" },
                values: new object[] { "[0,6,7,9,10,11,12,16,17]", "Full work-item and backlog access plus board and sprint management; no member/settings admin." });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000010"),
                column: "AllowedFunctions",
                value: "[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000011"),
                columns: new[] { "AllowedFunctions", "Description" },
                values: new object[] { "[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,17]", "Manages sprints, capacity, and team settings in addition to all work-item and backlog operations." });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000012"),
                columns: new[] { "AllowedFunctions", "Description" },
                values: new object[] { "[0,6,7,9,10,11,17]", "Creates and edits sprint tasks, manages the backlog, and can promote items to sprints." });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000013"),
                columns: new[] { "AllowedFunctions", "Description" },
                values: new object[] { "[0,6,7,10,17]", "Creates and edits sprint tasks and backlog items; focused on quality and test coverage." });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000014"),
                columns: new[] { "AllowedFunctions", "Description" },
                values: new object[] { "[0,10,17]", "Manages the product backlog and requirements; read-only on sprint execution." });

            // Remap any user-created custom roles from old single-digit values to new values.
            // Old→New: 3→6(CreateWorkItem), 4→7(EditWorkItem), 5→8(DeleteWorkItem),
            //          6→12(ManageSprint), 7→16(ManageBoard), 8→15(ManageWiki), 9→14(ManageCapacity).
            // Values 0,1,2 unchanged. Applied highest-to-lowest to prevent double-remapping.
            migrationBuilder.Sql("""
                UPDATE Roles SET AllowedFunctions =
                  REPLACE(REPLACE(REPLACE(REPLACE(
                  REPLACE(REPLACE(REPLACE(REPLACE(
                  REPLACE(REPLACE(REPLACE(REPLACE(
                  REPLACE(REPLACE(REPLACE(REPLACE(
                  REPLACE(REPLACE(REPLACE(REPLACE(
                  REPLACE(REPLACE(REPLACE(REPLACE(
                  REPLACE(REPLACE(REPLACE(REPLACE(
                    AllowedFunctions,
                    '[9]','[14]'),'[9,','[14,'),',9]',',14]'),',9,',',14,'),
                    '[8]','[15]'),'[8,','[15,'),',8]',',15]'),',8,',',15,'),
                    '[7]','[16]'),'[7,','[16,'),',7]',',16]'),',7,',',16,'),
                    '[6]','[12]'),'[6,','[12,'),',6]',',12]'),',6,',',12,'),
                    '[5]','[8]' ),'[5,','[8,' ),',5]',',8]' ),',5,',',8,' ),
                    '[4]','[7]' ),'[4,','[7,' ),',4]',',7]' ),',4,',',7,' ),
                    '[3]','[6]' ),'[3,','[6,' ),',3]',',6]' ),',3,',',6,' )
                WHERE IsDefault = 0
                  AND Id NOT IN (
                    '00000000-0000-0000-0003-000000000001'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000001"),
                columns: new[] { "AllowedFunctions", "Description" },
                values: new object[] { "[0,3,4,9,6]", "Full access to work items and board; read-only on settings." });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000010"),
                column: "AllowedFunctions",
                value: "[0,1,2,3,4,5,6,7,8,9]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000011"),
                columns: new[] { "AllowedFunctions", "Description" },
                values: new object[] { "[0,3,4,6,7,2]", "Manages sprints, capacity, and settings in addition to work items." });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000012"),
                columns: new[] { "AllowedFunctions", "Description" },
                values: new object[] { "[0,3,4]", "Create and edit work items." });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000013"),
                columns: new[] { "AllowedFunctions", "Description" },
                values: new object[] { "[0,3,4]", "Create and edit work items." });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000014"),
                columns: new[] { "AllowedFunctions", "Description" },
                values: new object[] { "[0,3,4]", "Create and edit work items." });
        }
    }
}
