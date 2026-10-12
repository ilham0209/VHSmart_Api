using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AudGroupAuditors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudGroupAuditors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudGroupAuditors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudGroupAuditors_AdmUsers_LeaderUserId",
                        column: x => x.LeaderUserId,
                        principalTable: "AdmUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudGroupAuditors_AudAuditChecklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalTable: "AudAuditChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudGroupAuditors_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AudGroupAuditorMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupAuditorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudGroupAuditorMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudGroupAuditorMembers_AdmUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AdmUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudGroupAuditorMembers_AudGroupAuditors_GroupAuditorId",
                        column: x => x.GroupAuditorId,
                        principalTable: "AudGroupAuditors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudGroupAuditorMembers_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudAuditPlans_GroupAuditorId",
                table: "AudAuditPlans",
                column: "GroupAuditorId");

            migrationBuilder.CreateIndex(
                name: "IX_AudGroupAuditorMembers_CompanyId",
                table: "AudGroupAuditorMembers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudGroupAuditorMembers_CompanyId_GroupAuditorId_UserId",
                table: "AudGroupAuditorMembers",
                columns: new[] { "CompanyId", "GroupAuditorId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AudGroupAuditorMembers_GroupAuditorId",
                table: "AudGroupAuditorMembers",
                column: "GroupAuditorId");

            migrationBuilder.CreateIndex(
                name: "IX_AudGroupAuditorMembers_UserId",
                table: "AudGroupAuditorMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AudGroupAuditors_ChecklistId",
                table: "AudGroupAuditors",
                column: "ChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_AudGroupAuditors_CompanyId",
                table: "AudGroupAuditors",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudGroupAuditors_LeaderUserId",
                table: "AudGroupAuditors",
                column: "LeaderUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AudAuditPlans_AudGroupAuditors_GroupAuditorId",
                table: "AudAuditPlans",
                column: "GroupAuditorId",
                principalTable: "AudGroupAuditors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AudAuditPlans_AudGroupAuditors_GroupAuditorId",
                table: "AudAuditPlans");

            migrationBuilder.DropTable(
                name: "AudGroupAuditorMembers");

            migrationBuilder.DropTable(
                name: "AudGroupAuditors");

            migrationBuilder.DropIndex(
                name: "IX_AudAuditPlans_GroupAuditorId",
                table: "AudAuditPlans");
        }
    }
}
