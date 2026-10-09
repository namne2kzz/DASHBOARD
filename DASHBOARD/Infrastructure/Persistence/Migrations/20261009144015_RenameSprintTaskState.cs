using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameSprintTaskState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Data migration: remap SprintTaskState integer values to WorkItemState ──
            // Old → New mapping:
            //   New(0)      → Open(0)       — no change
            //   Backlog(1)  → Open(0)       — standalone items unify under Open
            //   Todo(2)     → ToDo(1)
            //   Active(3)   → InProgress(2)
            //   InReview(4) → InReview(3)
            //   Done(5)     → Done(6)
            // Apply in reverse order of value to avoid double-mapping rows.
            migrationBuilder.Sql("UPDATE \"SprintTasks\" SET \"State\" = 6 WHERE \"State\" = 5;"); // Done(5) → Done(6)
            migrationBuilder.Sql("UPDATE \"SprintTasks\" SET \"State\" = 3 WHERE \"State\" = 4;"); // InReview(4) → InReview(3)
            migrationBuilder.Sql("UPDATE \"SprintTasks\" SET \"State\" = 2 WHERE \"State\" = 3;"); // Active(3) → InProgress(2)
            migrationBuilder.Sql("UPDATE \"SprintTasks\" SET \"State\" = 1 WHERE \"State\" = 2;"); // Todo(2) → ToDo(1)
            migrationBuilder.Sql("UPDATE \"SprintTasks\" SET \"State\" = 0 WHERE \"State\" = 1;"); // Backlog(1) → Open(0)
            // New(0) → Open(0): no SQL needed — value unchanged.

            // Remap SmartBoardColumns.MappedState for same integer shifts.
            migrationBuilder.Sql("UPDATE \"SmartBoardColumns\" SET \"MappedState\" = 6 WHERE \"MappedState\" = 5;");
            migrationBuilder.Sql("UPDATE \"SmartBoardColumns\" SET \"MappedState\" = 3 WHERE \"MappedState\" = 4;");
            migrationBuilder.Sql("UPDATE \"SmartBoardColumns\" SET \"MappedState\" = 2 WHERE \"MappedState\" = 3;");
            migrationBuilder.Sql("UPDATE \"SmartBoardColumns\" SET \"MappedState\" = 1 WHERE \"MappedState\" = 2;");
            migrationBuilder.Sql("UPDATE \"SmartBoardColumns\" SET \"MappedState\" = 0 WHERE \"MappedState\" = 1;");

            // ── Seed data updates (EF auto-generated — seed rows have fixed GUIDs) ──
            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000002"),
                column: "MappedState",
                value: 2);

            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000003"),
                column: "MappedState",
                value: 3);

            migrationBuilder.UpdateData(
                table: "SmartBoardColumns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-000a-000000000004"),
                column: "MappedState",
                value: 6);

            migrationBuilder.UpdateData(
                table: "SprintTasks",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000002"),
                column: "State",
                value: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Reverse data migration: remap WorkItemState back to SprintTaskState ──
            // Apply in forward order of value to avoid double-mapping rows.
            migrationBuilder.Sql("UPDATE \"SprintTasks\" SET \"State\" = 1 WHERE \"State\" = 0;"); // Open(0) → Backlog(1)
            migrationBuilder.Sql("UPDATE \"SprintTasks\" SET \"State\" = 2 WHERE \"State\" = 1;"); // ToDo(1) → Todo(2)
            migrationBuilder.Sql("UPDATE \"SprintTasks\" SET \"State\" = 3 WHERE \"State\" = 2;"); // InProgress(2) → Active(3)
            migrationBuilder.Sql("UPDATE \"SprintTasks\" SET \"State\" = 4 WHERE \"State\" = 3;"); // InReview(3) → InReview(4)
            migrationBuilder.Sql("UPDATE \"SprintTasks\" SET \"State\" = 5 WHERE \"State\" = 6;"); // Done(6) → Done(5)
            // Passed(7), Failed(8), Closed(9) are new — map to Done(5) as closest equivalent.
            migrationBuilder.Sql("UPDATE \"SprintTasks\" SET \"State\" = 5 WHERE \"State\" IN (7, 8, 9);");

            migrationBuilder.Sql("UPDATE \"SmartBoardColumns\" SET \"MappedState\" = 0 WHERE \"MappedState\" = 1;");
            migrationBuilder.Sql("UPDATE \"SmartBoardColumns\" SET \"MappedState\" = 1 WHERE \"MappedState\" = 2;");
            migrationBuilder.Sql("UPDATE \"SmartBoardColumns\" SET \"MappedState\" = 2 WHERE \"MappedState\" = 3;");
            migrationBuilder.Sql("UPDATE \"SmartBoardColumns\" SET \"MappedState\" = 3 WHERE \"MappedState\" = 4;");
            migrationBuilder.Sql("UPDATE \"SmartBoardColumns\" SET \"MappedState\" = 4 WHERE \"MappedState\" = 6;");
            migrationBuilder.Sql("UPDATE \"SmartBoardColumns\" SET \"MappedState\" = 5 WHERE \"MappedState\" IN (7, 8, 9);");

            // ── Seed data rollback (EF auto-generated) ──
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

            migrationBuilder.UpdateData(
                table: "SprintTasks",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0005-000000000002"),
                column: "State",
                value: 3);
        }
    }
}
