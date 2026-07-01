using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamRoleToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DefaultRole",
                table: "RepositoryMembers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "CapacityMembers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            // Convert the cast numeric strings ("0".."3") produced by AlterColumn into discipline labels
            // for all existing (non-seed) rows. Mapping mirrors the former TeamRole enum.
            migrationBuilder.Sql("UPDATE [RepositoryMembers] SET [DefaultRole] = CASE [DefaultRole] WHEN '0' THEN 'Developer' WHEN '1' THEN 'Tester' WHEN '2' THEN 'Scrum Master' WHEN '3' THEN 'Project Manager' ELSE [DefaultRole] END;");
            migrationBuilder.Sql("UPDATE [CapacityMembers] SET [Role] = CASE [Role] WHEN '0' THEN 'Developer' WHEN '1' THEN 'Tester' WHEN '2' THEN 'Scrum Master' WHEN '3' THEN 'Project Manager' ELSE [Role] END;");

            migrationBuilder.UpdateData(
                table: "CapacityMembers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0009-000000000001"),
                column: "Role",
                value: "Developer");

            migrationBuilder.UpdateData(
                table: "RepositoryMembers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000001"),
                column: "DefaultRole",
                value: "Scrum Master");

            migrationBuilder.UpdateData(
                table: "RepositoryMembers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000002"),
                column: "DefaultRole",
                value: "Developer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Map discipline labels back to numeric strings so the cast to int succeeds.
            migrationBuilder.Sql("UPDATE [RepositoryMembers] SET [DefaultRole] = CASE [DefaultRole] WHEN 'Developer' THEN '0' WHEN 'Tester' THEN '1' WHEN 'Scrum Master' THEN '2' WHEN 'Project Manager' THEN '3' ELSE '0' END;");
            migrationBuilder.Sql("UPDATE [CapacityMembers] SET [Role] = CASE [Role] WHEN 'Developer' THEN '0' WHEN 'Tester' THEN '1' WHEN 'Scrum Master' THEN '2' WHEN 'Project Manager' THEN '3' ELSE '0' END;");

            migrationBuilder.AlterColumn<int>(
                name: "DefaultRole",
                table: "RepositoryMembers",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "CapacityMembers",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.UpdateData(
                table: "CapacityMembers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0009-000000000001"),
                column: "Role",
                value: 0);

            migrationBuilder.UpdateData(
                table: "RepositoryMembers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000001"),
                column: "DefaultRole",
                value: 2);

            migrationBuilder.UpdateData(
                table: "RepositoryMembers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0004-000000000002"),
                column: "DefaultRole",
                value: 0);
        }
    }
}
