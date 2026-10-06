using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrdProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    Image_FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Image_StorageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Image_ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Image_SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrdProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrdProductImages_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdProductImages_PrdProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "PrdProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrdProductImages_CompanyId",
                table: "PrdProductImages",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdProductImages_ProductId",
                table: "PrdProductImages",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdProductImages_ProductId_Position",
                table: "PrdProductImages",
                columns: new[] { "ProductId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrdProductImages");
        }
    }
}
