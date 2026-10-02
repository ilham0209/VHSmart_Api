using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComPremiseAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComPremiseAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PremiseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "date", nullable: true),
                    ReferenceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_ComPremiseAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComPremiseAttachments_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComPremiseAttachments_ComPremises_PremiseId",
                        column: x => x.PremiseId,
                        principalTable: "ComPremises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComPremiseAttachments_CompanyId",
                table: "ComPremiseAttachments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremiseAttachments_PremiseId_DocumentType",
                table: "ComPremiseAttachments",
                columns: new[] { "PremiseId", "DocumentType" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComPremiseAttachments");
        }
    }
}
