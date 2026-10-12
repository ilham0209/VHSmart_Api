using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AudAuditTaskItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudAuditTaskItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceAuditCriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategorySequence = table.Column<int>(type: "int", nullable: false),
                    Criteria = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CriteriaSequence = table.Column<int>(type: "int", nullable: false),
                    SubCriteria = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PotentialPoint = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    ActualPoint = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    FinalPoint = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    IsConforming = table.Column<bool>(type: "bit", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudAuditTaskItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudAuditTaskItems_AudAuditCriteria_SourceAuditCriteriaId",
                        column: x => x.SourceAuditCriteriaId,
                        principalTable: "AudAuditCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditTaskItems_AudAuditTasks_AuditTaskId",
                        column: x => x.AuditTaskId,
                        principalTable: "AudAuditTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditTaskItems_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditTaskItems_AuditTaskId",
                table: "AudAuditTaskItems",
                column: "AuditTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditTaskItems_CompanyId",
                table: "AudAuditTaskItems",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditTaskItems_SourceAuditCriteriaId",
                table: "AudAuditTaskItems",
                column: "SourceAuditCriteriaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudAuditTaskItems");
        }
    }
}
