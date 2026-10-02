using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComMinutesMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComMinutesMeetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MeetingDate = table.Column<DateTime>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComMinutesMeetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComMinutesMeetings_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComMinutesMeetingAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinutesMeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Document_FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Document_StorageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Document_ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Document_SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComMinutesMeetingAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComMinutesMeetingAttachments_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComMinutesMeetingAttachments_ComMinutesMeetings_MinutesMeetingId",
                        column: x => x.MinutesMeetingId,
                        principalTable: "ComMinutesMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComMinutesMeetingAttachments_CompanyId",
                table: "ComMinutesMeetingAttachments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComMinutesMeetingAttachments_MinutesMeetingId",
                table: "ComMinutesMeetingAttachments",
                column: "MinutesMeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ComMinutesMeetings_CompanyId",
                table: "ComMinutesMeetings",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComMinutesMeetingAttachments");

            migrationBuilder.DropTable(
                name: "ComMinutesMeetings");
        }
    }
}
