using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AudAuditPrefixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudAuditPrefixes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudAuditPrefixes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudAuditPrefixes_AdmGeneralData_BrandId",
                        column: x => x.BrandId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditPrefixes_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditPrefixes_BrandId",
                table: "AudAuditPrefixes",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditPrefixes_CompanyId",
                table: "AudAuditPrefixes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditPrefixes_CompanyId_BrandId",
                table: "AudAuditPrefixes",
                columns: new[] { "CompanyId", "BrandId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudAuditPrefixes");
        }
    }
}
