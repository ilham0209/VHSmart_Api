using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RawMaterialAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RawMaterialAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawMaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "date", nullable: true),
                    DocumentStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ReferenceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Authority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_RawMaterialAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawMaterialAttachments_AdmSupportingDocuments_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "AdmSupportingDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawMaterialAttachments_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawMaterialAttachments_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterialAttachments_CompanyId",
                table: "RawMaterialAttachments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterialAttachments_DocumentTypeId",
                table: "RawMaterialAttachments",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterialAttachments_RawMaterialId",
                table: "RawMaterialAttachments",
                column: "RawMaterialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawMaterialAttachments");
        }
    }
}
