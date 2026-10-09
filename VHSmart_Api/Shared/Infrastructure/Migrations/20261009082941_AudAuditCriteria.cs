using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AudAuditCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudAuditCriteriaMasters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudAuditCriteriaMasters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudAuditCriteriaMasters_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AudAuditCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategorySequence = table.Column<int>(type: "int", nullable: false),
                    CriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriteriaSequence = table.Column<int>(type: "int", nullable: false),
                    SubCriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferenceCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PotentialPoint = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudAuditCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudAuditCriteria_AdmGeneralData_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditCriteria_AudAuditCriteriaMasters_CriteriaId",
                        column: x => x.CriteriaId,
                        principalTable: "AudAuditCriteriaMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditCriteria_AudAuditCriteriaMasters_SubCriteriaId",
                        column: x => x.SubCriteriaId,
                        principalTable: "AudAuditCriteriaMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditCriteria_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AudAuditCriteriaFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditCriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudAuditCriteriaFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudAuditCriteriaFindings_AudAuditCriteria_AuditCriteriaId",
                        column: x => x.AuditCriteriaId,
                        principalTable: "AudAuditCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditCriteriaFindings_AudFindings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "AudFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditCriteriaFindings_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditCriteria_CategoryId",
                table: "AudAuditCriteria",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditCriteria_CompanyId",
                table: "AudAuditCriteria",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditCriteria_CriteriaId",
                table: "AudAuditCriteria",
                column: "CriteriaId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditCriteria_SubCriteriaId",
                table: "AudAuditCriteria",
                column: "SubCriteriaId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditCriteriaFindings_AuditCriteriaId",
                table: "AudAuditCriteriaFindings",
                column: "AuditCriteriaId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditCriteriaFindings_CompanyId",
                table: "AudAuditCriteriaFindings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditCriteriaFindings_FindingId",
                table: "AudAuditCriteriaFindings",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditCriteriaMasters_CompanyId",
                table: "AudAuditCriteriaMasters",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditCriteriaMasters_CompanyId_Kind_Text",
                table: "AudAuditCriteriaMasters",
                columns: new[] { "CompanyId", "Kind", "Text" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudAuditCriteriaFindings");

            migrationBuilder.DropTable(
                name: "AudAuditCriteria");

            migrationBuilder.DropTable(
                name: "AudAuditCriteriaMasters");
        }
    }
}
