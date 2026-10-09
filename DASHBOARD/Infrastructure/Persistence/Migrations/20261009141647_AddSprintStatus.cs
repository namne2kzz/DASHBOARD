using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Sprints",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Sprints",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Planning");

            migrationBuilder.UpdateData(
                table: "Sprints",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0007-000000000001"),
                column: "ClosedAt",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_Repository_OneActive",
                table: "Sprints",
                columns: new[] { "RepositoryId", "Status" },
                unique: true,
                filter: "\"Status\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sprints_Repository_OneActive",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Sprints");
        }
    }
}
