using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AudAuditTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudAuditTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartAuditDate = table.Column<DateTime>(type: "date", nullable: true),
                    TimeIn = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndAuditDate = table.Column<DateTime>(type: "date", nullable: true),
                    TimeOut = table.Column<TimeSpan>(type: "time", nullable: true),
                    AuditScore = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    FinalReviewScore = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AuditeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AuditeeDesignation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReportingTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudAuditTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudAuditTasks_AudAuditPlans_AuditPlanId",
                        column: x => x.AuditPlanId,
                        principalTable: "AudAuditPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudAuditTasks_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditTasks_AuditPlanId",
                table: "AudAuditTasks",
                column: "AuditPlanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditTasks_CompanyId",
                table: "AudAuditTasks",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudAuditTasks");
        }
    }
}
