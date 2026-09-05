using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWiki : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WikiPages");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000010"),
                column: "AllowedFunctions",
                value: "[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,16,17,18,19,20]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000011"),
                column: "AllowedFunctions",
                value: "[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,16,17,18,19,20]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000012"),
                column: "AllowedFunctions",
                value: "[0,6,7,9,17,18,19]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000013"),
                column: "AllowedFunctions",
                value: "[0,6,7,9,10,17]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000014"),
                column: "AllowedFunctions",
                value: "[0,6,7,8,9,10,11,12,13,17]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WikiPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WikiPages_WikiPages_ParentId",
                        column: x => x.ParentId,
                        principalTable: "WikiPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000010"),
                column: "AllowedFunctions",
                value: "[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000011"),
                column: "AllowedFunctions",
                value: "[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000012"),
                column: "AllowedFunctions",
                value: "[0,6,7,9,15,17,18,19]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000013"),
                column: "AllowedFunctions",
                value: "[0,6,7,9,10,15,17]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000014"),
                column: "AllowedFunctions",
                value: "[0,6,7,8,9,10,11,12,13,15,17]");

            migrationBuilder.InsertData(
                table: "WikiPages",
                columns: new[] { "Id", "Content", "CreatedAt", "DeletedAt", "DeletedByUserId", "LastUpdated", "ParentId", "RepositoryId", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0006-000000000001"), "<h1>Architecture</h1><p>Full-stack .NET 10 + Angular v19 application.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("00000000-0000-0000-0002-000000000001"), "System Architecture Overview", null },
                    { new Guid("00000000-0000-0000-0006-000000000002"), "<h2>Auth</h2><p>JWT Bearer tokens with PBKDF2-SHA512 password hashing.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0006-000000000001"), new Guid("00000000-0000-0000-0002-000000000001"), "Authentication Flow", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_IsDeleted",
                table: "WikiPages",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_ParentId",
                table: "WikiPages",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_RepositoryId",
                table: "WikiPages",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_RepositoryId_ParentId",
                table: "WikiPages",
                columns: new[] { "RepositoryId", "ParentId" });
        }
    }
}
