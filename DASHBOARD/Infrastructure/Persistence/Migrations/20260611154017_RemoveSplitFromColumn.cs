using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSplitFromColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Split",
                table: "SmartBoardColumns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Split",
                table: "SmartBoardColumns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000001"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000002"),
                column: "Split",
                value: true);

            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000003"),
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000004"),
                columns: new string[0],
                values: new object[0]);
        }
    }
}
