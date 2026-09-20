using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintTaskUserStoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "SprintTasks",
                type: "nvarchar(max)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Documents",
                table: "SprintTasks",
                type: "nvarchar(max)",
                maxLength: 10000,
                nullable: false,
                defaultValueSql: "'[]'");

            migrationBuilder.UpdateData(
                table: "SprintTasks",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000002"),
                column: "AcceptanceCriteria",
                value: null);

            migrationBuilder.UpdateData(
                table: "SprintTasks",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000005"),
                column: "AcceptanceCriteria",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "SprintTasks");

            migrationBuilder.DropColumn(
                name: "Documents",
                table: "SprintTasks");
        }
    }
}
