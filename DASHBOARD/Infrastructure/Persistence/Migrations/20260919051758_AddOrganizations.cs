using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DASHBOARD.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Introduces the Organization tenant: the table itself, an <c>OrgId</c> on Users and
    /// Repositories, and the uniqueness rules that become per-tenant as a result.
    /// </summary>
    /// <remarks>
    /// This migration was committed empty, so a database built from the migration chain had no
    /// Organizations table and no OrgId columns at all, while the entity model and every handler
    /// assumed both. The contents below were reconstructed from the live schema and from
    /// <c>OrganizationConfiguration</c>, <c>UserConfiguration</c> and <c>SeedData</c>.
    ///
    /// Existing rows predate tenancy, so <c>OrgId</c> is added NOT NULL with a zero-GUID default and
    /// then pointed at the seeded default organization — the same shape the live database carries.
    /// </remarks>
    public partial class AddOrganizations : Migration
    {
        private static readonly Guid DefaultOrgId = new("00000000-0000-0000-0009-000000000001");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── The tenant table ─────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id                  = table.Column<Guid>(nullable: false),
                    Name                = table.Column<string>(maxLength: 200,  nullable: false),
                    Alias               = table.Column<string>(maxLength: 50,   nullable: false),
                    ContactEmail        = table.Column<string>(maxLength: 320,  nullable: false),
                    About               = table.Column<string>(maxLength: 2000, nullable: false),
                    LicenseKey          = table.Column<string>(maxLength: 500,  nullable: false),
                    LicenseDueDate      = table.Column<DateTime>(nullable: false),
                    LicenseExpireDate   = table.Column<DateTime>(nullable: false),
                    LicenseRepoCapacity = table.Column<int>(nullable: false),
                    CreatedAt           = table.Column<DateTime>(nullable: false),
                    UpdatedAt           = table.Column<DateTime>(nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Alias",
                table: "Organizations",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_LicenseKey",
                table: "Organizations",
                column: "LicenseKey",
                unique: true);

            // ── The default tenant that adopts all pre-existing data ─────────
            // Inserted before the foreign keys below, so the back-fill has a row to point at.
            migrationBuilder.InsertData(
                table: "Organizations",
                columns: ["Id", "Name", "Alias", "ContactEmail", "About", "LicenseKey",
                          "LicenseDueDate", "LicenseExpireDate", "LicenseRepoCapacity",
                          "CreatedAt", "UpdatedAt"],
                values:
                [
                    DefaultOrgId,
                    "Default Organization",
                    "default",
                    "admin@dashboard.local",
                    "Default organization created for pre-existing repositories and users.",
                    "TkVYLTAwMDEtUFJPRA==",
                    new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                    20,
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    null,
                ]);

            // ── Tenancy columns ─────────────────────────────────────────────
            migrationBuilder.AddColumn<Guid>(
                name: "OrgId",
                table: "Users",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "OrgId",
                table: "Repositories",
                nullable: false,
                defaultValue: Guid.Empty);

            // Adopt every row that existed before tenancy into the default organization.
            migrationBuilder.Sql(
                $"UPDATE [Users] SET [OrgId] = '{DefaultOrgId}' WHERE [OrgId] = '{Guid.Empty}';");
            migrationBuilder.Sql(
                $"UPDATE [Repositories] SET [OrgId] = '{DefaultOrgId}' WHERE [OrgId] = '{Guid.Empty}';");

            // ── Uniqueness becomes per-tenant ───────────────────────────────
            // An email or a repository code only has to be unique inside one organization, so the
            // single-column unique indexes from InitialCreated are replaced by composites.
            migrationBuilder.DropIndex(name: "IX_Users_Email",        table: "Users");
            migrationBuilder.DropIndex(name: "IX_Repositories_Code",  table: "Repositories");

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrgId_Email",
                table: "Users",
                columns: ["OrgId", "Email"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_OrgId_Code",
                table: "Repositories",
                columns: ["OrgId", "Code"],
                unique: true);

            // ── Foreign keys ────────────────────────────────────────────────
            // Restrict: deleting an organization with users or repositories still in it must fail
            // rather than cascade away a whole tenant's data.
            migrationBuilder.AddForeignKey(
                name: "FK_Users_Organizations_OrgId",
                table: "Users",
                column: "OrgId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Repositories_Organizations_OrgId",
                table: "Repositories",
                column: "OrgId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Repositories_Organizations_OrgId", table: "Repositories");
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Organizations_OrgId", table: "Users");

            migrationBuilder.DropIndex(name: "IX_Repositories_OrgId_Code", table: "Repositories");
            migrationBuilder.DropIndex(name: "IX_Users_OrgId_Email",       table: "Users");

            migrationBuilder.DropColumn(name: "OrgId", table: "Repositories");
            migrationBuilder.DropColumn(name: "OrgId", table: "Users");

            migrationBuilder.DropTable(name: "Organizations");

            // Restore the pre-tenancy uniqueness rules.
            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Code",
                table: "Repositories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }
    }
}
