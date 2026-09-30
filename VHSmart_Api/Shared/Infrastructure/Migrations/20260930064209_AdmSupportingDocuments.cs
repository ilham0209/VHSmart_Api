using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdmSupportingDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdmSupportingDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ForView = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocumentSequence = table.Column<int>(type: "int", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Template_FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Template_StorageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Template_ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Template_SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmSupportingDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmSupportingDocuments_CompanyId",
                table: "AdmSupportingDocuments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmSupportingDocuments_CompanyId_ForView_DocumentSequence",
                table: "AdmSupportingDocuments",
                columns: new[] { "CompanyId", "ForView", "DocumentSequence" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmSupportingDocuments");
        }
    }
}
