using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSprintIsActiveColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sprints_RepositoryId_IsActive",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Sprints");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Sprints",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Sprints",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0007-000000000001"),
                column: "IsActive",
                value: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_RepositoryId_IsActive",
                table: "Sprints",
                columns: new[] { "RepositoryId", "IsActive" });
        }
    }
}
