using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMetadataIsGlobal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "RepositoryId",
                table: "RepositoryMetadata",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<bool>(
                name: "IsGlobal",
                table: "RepositoryMetadata",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "RepositoryMetadata",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "DeletedByUserId", "IsGlobal", "Key", "RepositoryId", "UpdatedAt", "Value" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-000b-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "RepoRole", null, null, "Developer" },
                    { new Guid("00000000-0000-0000-000b-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "RepoRole", null, null, "Tester" },
                    { new Guid("00000000-0000-0000-000b-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "RepoRole", null, null, "Scrum Master" },
                    { new Guid("00000000-0000-0000-000b-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "RepoRole", null, null, "Project Manager" },
                    { new Guid("00000000-0000-0000-000b-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, true, "RepoRole", null, null, "Business Analyst" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000001"));

            migrationBuilder.DeleteData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000002"));

            migrationBuilder.DeleteData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000003"));

            migrationBuilder.DeleteData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000004"));

            migrationBuilder.DeleteData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000005"));

            migrationBuilder.DropColumn(
                name: "IsGlobal",
                table: "RepositoryMetadata");

            migrationBuilder.AlterColumn<Guid>(
                name: "RepositoryId",
                table: "RepositoryMetadata",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
