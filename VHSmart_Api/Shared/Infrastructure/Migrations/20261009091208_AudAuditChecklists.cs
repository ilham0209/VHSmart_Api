using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AudAuditChecklists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudAuditChecklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudAuditChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudAuditChecklists_AdmGeneralData_ChecklistCategoryId",
                        column: x => x.ChecklistCategoryId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditChecklists_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AudAuditChecklistCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditCriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudAuditChecklistCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudAuditChecklistCriteria_AudAuditChecklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalTable: "AudAuditChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditChecklistCriteria_AudAuditCriteria_AuditCriteriaId",
                        column: x => x.AuditCriteriaId,
                        principalTable: "AudAuditCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditChecklistCriteria_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AudAuditPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditReferenceNo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AuditPurposeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PremiseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleDate = table.Column<DateTime>(type: "date", nullable: false),
                    GroupAuditorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateAssigned = table.Column<DateTime>(type: "date", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudAuditPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudAuditPlans_AdmGeneralData_AuditPurposeId",
                        column: x => x.AuditPurposeId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditPlans_AdmGeneralData_AuditTypeId",
                        column: x => x.AuditTypeId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditPlans_AdmGeneralData_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditPlans_AdmUsers_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "AdmUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditPlans_AudAuditChecklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalTable: "AudAuditChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditPlans_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditPlans_ComPremises_PremiseId",
                        column: x => x.PremiseId,
                        principalTable: "ComPremises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditChecklistCriteria_AuditCriteriaId",
                table: "AudAuditChecklistCriteria",
                column: "AuditCriteriaId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditChecklistCriteria_ChecklistId",
                table: "AudAuditChecklistCriteria",
                column: "ChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditChecklistCriteria_CompanyId",
                table: "AudAuditChecklistCriteria",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditChecklistCriteria_CompanyId_ChecklistId_AuditCriteriaId",
                table: "AudAuditChecklistCriteria",
                columns: new[] { "CompanyId", "ChecklistId", "AuditCriteriaId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditChecklists_ChecklistCategoryId",
                table: "AudAuditChecklists",
                column: "ChecklistCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditChecklists_CompanyId",
                table: "AudAuditChecklists",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditPlans_AssignedByUserId",
                table: "AudAuditPlans",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditPlans_AuditPurposeId",
                table: "AudAuditPlans",
                column: "AuditPurposeId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditPlans_AuditTypeId",
                table: "AudAuditPlans",
                column: "AuditTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditPlans_CategoryId",
                table: "AudAuditPlans",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditPlans_ChecklistId",
                table: "AudAuditPlans",
                column: "ChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditPlans_CompanyId",
                table: "AudAuditPlans",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditPlans_CompanyId_AuditReferenceNo",
                table: "AudAuditPlans",
                columns: new[] { "CompanyId", "AuditReferenceNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditPlans_PremiseId",
                table: "AudAuditPlans",
                column: "PremiseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudAuditChecklistCriteria");

            migrationBuilder.DropTable(
                name: "AudAuditPlans");

            migrationBuilder.DropTable(
                name: "AudAuditChecklists");
        }
    }
}
