using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeMemberRoleRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safety backfill: link any member still without a role to the default role
            // matching their team role (DefaultRole: Dev=0, Tester=1, ScrumMaster=2, ProjectManager=3).
            migrationBuilder.Sql("UPDATE [RepositoryMembers] SET [RoleId] = '00000000-0000-0000-0003-000000000012' WHERE [RoleId] IS NULL AND [DefaultRole] = 0;");
            migrationBuilder.Sql("UPDATE [RepositoryMembers] SET [RoleId] = '00000000-0000-0000-0003-000000000013' WHERE [RoleId] IS NULL AND [DefaultRole] = 1;");
            migrationBuilder.Sql("UPDATE [RepositoryMembers] SET [RoleId] = '00000000-0000-0000-0003-000000000010' WHERE [RoleId] IS NULL AND [DefaultRole] = 2;");
            migrationBuilder.Sql("UPDATE [RepositoryMembers] SET [RoleId] = '00000000-0000-0000-0003-000000000011' WHERE [RoleId] IS NULL AND [DefaultRole] = 3;");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "RepositoryMembers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "RepositoryMembers",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }
    }
}
