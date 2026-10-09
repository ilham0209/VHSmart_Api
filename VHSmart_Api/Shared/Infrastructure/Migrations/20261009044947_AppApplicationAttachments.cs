using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AppApplicationAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppApplicationAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_AppApplicationAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppApplicationAttachments_AdmSupportingDocuments_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "AdmSupportingDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppApplicationAttachments_AppHalalApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "AppHalalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppApplicationAttachments_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppApplicationAttachments_ApplicationId",
                table: "AppApplicationAttachments",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppApplicationAttachments_CompanyId",
                table: "AppApplicationAttachments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppApplicationAttachments_DocumentTypeId",
                table: "AppApplicationAttachments",
                column: "DocumentTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppApplicationAttachments");
        }
    }
}
