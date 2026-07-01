using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerRepoDefaultRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000001"),
                columns: new[] { "IsGlobal", "RepositoryId" },
                values: new object[] { false, new Guid("00000000-0000-0000-0002-000000000001") });

            migrationBuilder.UpdateData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000002"),
                columns: new[] { "IsGlobal", "RepositoryId" },
                values: new object[] { false, new Guid("00000000-0000-0000-0002-000000000001") });

            migrationBuilder.UpdateData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000003"),
                columns: new[] { "IsGlobal", "RepositoryId" },
                values: new object[] { false, new Guid("00000000-0000-0000-0002-000000000001") });

            migrationBuilder.UpdateData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000004"),
                columns: new[] { "IsGlobal", "RepositoryId" },
                values: new object[] { false, new Guid("00000000-0000-0000-0002-000000000001") });

            migrationBuilder.UpdateData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000005"),
                columns: new[] { "IsGlobal", "RepositoryId" },
                values: new object[] { false, new Guid("00000000-0000-0000-0002-000000000001") });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000010"),
                column: "RepositoryId",
                value: new Guid("00000000-0000-0000-0002-000000000001"));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000011"),
                column: "RepositoryId",
                value: new Guid("00000000-0000-0000-0002-000000000001"));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000012"),
                column: "RepositoryId",
                value: new Guid("00000000-0000-0000-0002-000000000001"));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000013"),
                column: "RepositoryId",
                value: new Guid("00000000-0000-0000-0002-000000000001"));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000014"),
                column: "RepositoryId",
                value: new Guid("00000000-0000-0000-0002-000000000001"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000001"),
                columns: new[] { "IsGlobal", "RepositoryId" },
                values: new object[] { true, null });

            migrationBuilder.UpdateData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000002"),
                columns: new[] { "IsGlobal", "RepositoryId" },
                values: new object[] { true, null });

            migrationBuilder.UpdateData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000003"),
                columns: new[] { "IsGlobal", "RepositoryId" },
                values: new object[] { true, null });

            migrationBuilder.UpdateData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000004"),
                columns: new[] { "IsGlobal", "RepositoryId" },
                values: new object[] { true, null });

            migrationBuilder.UpdateData(
                table: "RepositoryMetadata",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000b-000000000005"),
                columns: new[] { "IsGlobal", "RepositoryId" },
                values: new object[] { true, null });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000010"),
                column: "RepositoryId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000011"),
                column: "RepositoryId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000012"),
                column: "RepositoryId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000013"),
                column: "RepositoryId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000014"),
                column: "RepositoryId",
                value: null);
        }
    }
}
