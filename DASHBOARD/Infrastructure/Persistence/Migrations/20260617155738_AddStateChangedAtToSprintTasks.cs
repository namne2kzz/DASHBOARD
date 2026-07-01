using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStateChangedAtToSprintTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StateChangedAt",
                table: "SprintTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SprintTasks",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000002"),
                column: "StateChangedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "SprintTasks",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000005"),
                column: "StateChangedAt",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StateChangedAt",
                table: "SprintTasks");
        }
    }
}
