using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AudCorrectiveActionEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudCorrectiveActionEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NonConformanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FilledBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StatusSet = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Attachment_FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Attachment_StorageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Attachment_ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Attachment_SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    FilledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudCorrectiveActionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudCorrectiveActionEntries_AdmUsers_FilledByUserId",
                        column: x => x.FilledByUserId,
                        principalTable: "AdmUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudCorrectiveActionEntries_AudNonConformances_NonConformanceId",
                        column: x => x.NonConformanceId,
                        principalTable: "AudNonConformances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudCorrectiveActionEntries_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudCorrectiveActionEntries_CompanyId",
                table: "AudCorrectiveActionEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudCorrectiveActionEntries_FilledByUserId",
                table: "AudCorrectiveActionEntries",
                column: "FilledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AudCorrectiveActionEntries_NonConformanceId",
                table: "AudCorrectiveActionEntries",
                column: "NonConformanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudCorrectiveActionEntries");
        }
    }
}
