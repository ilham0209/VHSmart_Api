using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PrdProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrdProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ManufacturerSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Gtin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NutritionContentClaims = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PotentialAllergens = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CalorieContent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AvailableAt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PackagingSize = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MarketingMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SchemeSpecificData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QrCodeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VerifyHalalPublishStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrdProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrdProducts_AdmGeneralData_BrandId",
                        column: x => x.BrandId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdProducts_AdmGeneralData_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdProducts_AdmGeneralData_MarketingMethodId",
                        column: x => x.MarketingMethodId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdProducts_AdmSchemes_SchemeId",
                        column: x => x.SchemeId,
                        principalTable: "AdmSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdProducts_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdProducts_RawManufacturerSuppliers_ManufacturerSupplierId",
                        column: x => x.ManufacturerSupplierId,
                        principalTable: "RawManufacturerSuppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrdProducts_BrandId",
                table: "PrdProducts",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdProducts_CategoryId",
                table: "PrdProducts",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdProducts_CompanyId",
                table: "PrdProducts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdProducts_ManufacturerSupplierId",
                table: "PrdProducts",
                column: "ManufacturerSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdProducts_MarketingMethodId",
                table: "PrdProducts",
                column: "MarketingMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdProducts_SchemeId",
                table: "PrdProducts",
                column: "SchemeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrdProducts");
        }
    }
}
