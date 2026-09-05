using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintChannelLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SprintChannelLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HubChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HubChannelUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintChannelLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintChannelLinks_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SprintChannelLinks_HubChannelId",
                table: "SprintChannelLinks",
                column: "HubChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintChannelLinks_SprintId",
                table: "SprintChannelLinks",
                column: "SprintId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SprintChannelLinks");
        }
    }
}
