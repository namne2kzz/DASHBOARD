using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItemMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SprintTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetadataId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemMetadata_RepositoryMetadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "RepositoryMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemMetadata_SprintTasks_SprintTaskId",
                        column: x => x.SprintTaskId,
                        principalTable: "SprintTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemMetadata_MetadataId",
                table: "WorkItemMetadata",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemMetadata_SprintTaskId_MetadataId",
                table: "WorkItemMetadata",
                columns: new[] { "SprintTaskId", "MetadataId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemMetadata");
        }
    }
}
