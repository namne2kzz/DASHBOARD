using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmartBoardCards");

            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000002"),
                column: "MappedState",
                value: 3);

            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000003"),
                column: "MappedState",
                value: 4);

            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000004"),
                column: "MappedState",
                value: 5);

            migrationBuilder.CreateIndex(
                name: "IX_SmartBoardColumns_RepositoryId_MappedState",
                table: "SmartBoardColumns",
                columns: new[] { "RepositoryId", "MappedState" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SmartBoardColumns_RepositoryId_MappedState",
                table: "SmartBoardColumns");

            migrationBuilder.CreateTable(
                name: "SmartBoardCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColumnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SprintTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnteredColumnAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SplitSide = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    SwimlaneId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartBoardCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmartBoardCards_SmartBoardColumns_ColumnId",
                        column: x => x.ColumnId,
                        principalTable: "SmartBoardColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmartBoardCards_SprintTasks_SprintTaskId",
                        column: x => x.SprintTaskId,
                        principalTable: "SprintTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000002"),
                column: "MappedState",
                value: 1);

            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000003"),
                column: "MappedState",
                value: 2);

            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000004"),
                column: "MappedState",
                value: 3);

            migrationBuilder.CreateIndex(
                name: "IX_SmartBoardCards_ColumnId",
                table: "SmartBoardCards",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartBoardCards_RepositoryId",
                table: "SmartBoardCards",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartBoardCards_SprintTaskId",
                table: "SmartBoardCards",
                column: "SprintTaskId");
        }
    }
}
