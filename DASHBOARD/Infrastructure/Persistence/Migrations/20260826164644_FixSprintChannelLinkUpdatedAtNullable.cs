using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixSprintChannelLinkUpdatedAtNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The original AddSprintChannelLink migration created UpdatedAt as NOT NULL while the
            // model (BaseEntity.UpdatedAt is DateTime?) and snapshot treat it as nullable. That
            // mismatch made every INSERT fail (SQL 515) because EF sends UpdatedAt = NULL.
            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "SprintChannelLinks",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "SprintChannelLinks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
