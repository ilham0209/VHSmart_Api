using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AudExternalAuditReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudExternalAuditReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditReferenceNo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ReceivedFromCertificationBodyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CbAuditReferenceNo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TypeOfApplication = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TypeOfInspection = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApplicationSchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CbHalalApplicationNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CertificationNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AuditDate = table.Column<DateTime>(type: "date", nullable: false),
                    TimeIn = table.Column<TimeSpan>(type: "time", nullable: false),
                    TimeOut = table.Column<TimeSpan>(type: "time", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OverallStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudExternalAuditReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudExternalAuditReports_AdmCertificationBodies_ReceivedFromCertificationBodyId",
                        column: x => x.ReceivedFromCertificationBodyId,
                        principalTable: "AdmCertificationBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudExternalAuditReports_AdmSchemes_ApplicationSchemeId",
                        column: x => x.ApplicationSchemeId,
                        principalTable: "AdmSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudExternalAuditReports_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AudExternalAuditReportAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalAuditReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Document_FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Document_StorageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Document_ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Document_SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudExternalAuditReportAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudExternalAuditReportAttachments_AudExternalAuditReports_ExternalAuditReportId",
                        column: x => x.ExternalAuditReportId,
                        principalTable: "AudExternalAuditReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudExternalAuditReportAttachments_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AudExternalAuditReportPremises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalAuditReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PremiseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudExternalAuditReportPremises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudExternalAuditReportPremises_AudExternalAuditReports_ExternalAuditReportId",
                        column: x => x.ExternalAuditReportId,
                        principalTable: "AudExternalAuditReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudExternalAuditReportPremises_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudExternalAuditReportPremises_ComPremises_PremiseId",
                        column: x => x.PremiseId,
                        principalTable: "ComPremises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AudNonConformances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AuditReferenceNo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AuditTaskItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalAuditReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PremiseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DetailName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Clause = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Findings = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NonConformanceDetails = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudNonConformances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudNonConformances_AdmGeneralData_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudNonConformances_AdmUsers_ClosedBy",
                        column: x => x.ClosedBy,
                        principalTable: "AdmUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudNonConformances_AudAuditTaskItems_AuditTaskItemId",
                        column: x => x.AuditTaskItemId,
                        principalTable: "AudAuditTaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudNonConformances_AudExternalAuditReports_ExternalAuditReportId",
                        column: x => x.ExternalAuditReportId,
                        principalTable: "AudExternalAuditReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudNonConformances_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudNonConformances_ComPremises_PremiseId",
                        column: x => x.PremiseId,
                        principalTable: "ComPremises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudExternalAuditReportAttachments_CompanyId",
                table: "AudExternalAuditReportAttachments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudExternalAuditReportAttachments_ExternalAuditReportId",
                table: "AudExternalAuditReportAttachments",
                column: "ExternalAuditReportId");

            migrationBuilder.CreateIndex(
                name: "IX_AudExternalAuditReportPremises_CompanyId",
                table: "AudExternalAuditReportPremises",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudExternalAuditReportPremises_CompanyId_ExternalAuditReportId_PremiseId",
                table: "AudExternalAuditReportPremises",
                columns: new[] { "CompanyId", "ExternalAuditReportId", "PremiseId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AudExternalAuditReportPremises_ExternalAuditReportId",
                table: "AudExternalAuditReportPremises",
                column: "ExternalAuditReportId");

            migrationBuilder.CreateIndex(
                name: "IX_AudExternalAuditReportPremises_PremiseId",
                table: "AudExternalAuditReportPremises",
                column: "PremiseId");

            migrationBuilder.CreateIndex(
                name: "IX_AudExternalAuditReports_ApplicationSchemeId",
                table: "AudExternalAuditReports",
                column: "ApplicationSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_AudExternalAuditReports_CompanyId",
                table: "AudExternalAuditReports",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudExternalAuditReports_ReceivedFromCertificationBodyId",
                table: "AudExternalAuditReports",
                column: "ReceivedFromCertificationBodyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudNonConformances_AuditTaskItemId",
                table: "AudNonConformances",
                column: "AuditTaskItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AudNonConformances_CategoryId",
                table: "AudNonConformances",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AudNonConformances_ClosedBy",
                table: "AudNonConformances",
                column: "ClosedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AudNonConformances_CompanyId",
                table: "AudNonConformances",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudNonConformances_ExternalAuditReportId",
                table: "AudNonConformances",
                column: "ExternalAuditReportId");

            migrationBuilder.CreateIndex(
                name: "IX_AudNonConformances_PremiseId",
                table: "AudNonConformances",
                column: "PremiseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudExternalAuditReportAttachments");

            migrationBuilder.DropTable(
                name: "AudExternalAuditReportPremises");

            migrationBuilder.DropTable(
                name: "AudNonConformances");

            migrationBuilder.DropTable(
                name: "AudExternalAuditReports");
        }
    }
}
