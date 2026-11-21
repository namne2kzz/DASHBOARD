using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussionReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "DiscussionEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionEntries_ParentId",
                table: "DiscussionEntries",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionEntries_SprintTaskId_ParentId",
                table: "DiscussionEntries",
                columns: new[] { "SprintTaskId", "ParentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionEntries_DiscussionEntries_ParentId",
                table: "DiscussionEntries",
                column: "ParentId",
                principalTable: "DiscussionEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionEntries_DiscussionEntries_ParentId",
                table: "DiscussionEntries");

            migrationBuilder.DropIndex(
                name: "IX_DiscussionEntries_ParentId",
                table: "DiscussionEntries");

            migrationBuilder.DropIndex(
                name: "IX_DiscussionEntries_SprintTaskId_ParentId",
                table: "DiscussionEntries");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "DiscussionEntries");
        }
    }
}
