using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BacklogItem_ReplaceIterationWithSprintId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Iteration",
                table: "BacklogItems");

            migrationBuilder.AddColumn<Guid>(
                name: "SprintId",
                table: "BacklogItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "BacklogItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0008-000000000001"),
                column: "SprintId",
                value: null);

            migrationBuilder.UpdateData(
                table: "BacklogItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0008-000000000002"),
                column: "SprintId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_BacklogItems_SprintId",
                table: "BacklogItems",
                column: "SprintId");

            migrationBuilder.AddForeignKey(
                name: "FK_BacklogItems_Sprints_SprintId",
                table: "BacklogItems",
                column: "SprintId",
                principalTable: "Sprints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BacklogItems_Sprints_SprintId",
                table: "BacklogItems");

            migrationBuilder.DropIndex(
                name: "IX_BacklogItems_SprintId",
                table: "BacklogItems");

            migrationBuilder.DropColumn(
                name: "SprintId",
                table: "BacklogItems");

            migrationBuilder.AddColumn<string>(
                name: "Iteration",
                table: "BacklogItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "BacklogItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0008-000000000001"),
                column: "Iteration",
                value: null);

            migrationBuilder.UpdateData(
                table: "BacklogItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0008-000000000002"),
                column: "Iteration",
                value: "May 2026");
        }
    }
}
