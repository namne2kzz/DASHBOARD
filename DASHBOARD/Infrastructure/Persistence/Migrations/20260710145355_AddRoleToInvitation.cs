using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleToInvitation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultRole",
                table: "Invitations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "Invitations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000011"),
                column: "AllowedFunctions",
                value: "[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19]");

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

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_RoleId",
                table: "Invitations",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Roles_RoleId",
                table: "Invitations",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Roles_RoleId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_RoleId",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "DefaultRole",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "Invitations");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000011"),
                column: "AllowedFunctions",
                value: "[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,17]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000012"),
                column: "AllowedFunctions",
                value: "[0,6,7,9,10,11,17]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000013"),
                column: "AllowedFunctions",
                value: "[0,6,7,10,17]");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0003-000000000014"),
                column: "AllowedFunctions",
                value: "[0,10,17]");
        }
    }
}
