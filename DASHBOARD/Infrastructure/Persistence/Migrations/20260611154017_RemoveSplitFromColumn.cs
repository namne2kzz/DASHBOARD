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

            // Only the second seed column had Split set. The scaffolder also emitted an UpdateData
            // for each of the other three with an empty column list, which generates the literal
            // text "UPDATE [SmartBoardColumns] SET " followed by the WHERE clause — invalid SQL
            // that fails the whole rollback with "Incorrect syntax near the keyword 'WHERE'".
            // Those rows take false from the column default added above, so there is nothing to
            // write and the statements are simply dropped.
            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000002"),
                column: "Split",
                value: true);
        }
    }
}
