using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RawMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RawMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IngredientStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ingredient = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IngredientCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CommercialName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ScientificName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IngredientSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManufacturerSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPackagingMaterial = table.Column<bool>(type: "bit", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawMaterials_AdmGeneralData_IngredientSourceId",
                        column: x => x.IngredientSourceId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawMaterials_AdmGeneralData_IngredientStatusId",
                        column: x => x.IngredientStatusId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawMaterials_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawMaterials_RawManufacturerSuppliers_ManufacturerSupplierId",
                        column: x => x.ManufacturerSupplierId,
                        principalTable: "RawManufacturerSuppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RawMaterialAccessibleCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawMaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessibleCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawMaterialAccessibleCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawMaterialAccessibleCompanies_ComCompanies_AccessibleCompanyId",
                        column: x => x.AccessibleCompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawMaterialAccessibleCompanies_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterialAccessibleCompanies_AccessibleCompanyId",
                table: "RawMaterialAccessibleCompanies",
                column: "AccessibleCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterialAccessibleCompanies_RawMaterialId",
                table: "RawMaterialAccessibleCompanies",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterials_CompanyId",
                table: "RawMaterials",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterials_CompanyId_IngredientCode",
                table: "RawMaterials",
                columns: new[] { "CompanyId", "IngredientCode" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [IngredientCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterials_IngredientSourceId",
                table: "RawMaterials",
                column: "IngredientSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterials_IngredientStatusId",
                table: "RawMaterials",
                column: "IngredientStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterials_ManufacturerSupplierId",
                table: "RawMaterials",
                column: "ManufacturerSupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawMaterialAccessibleCompanies");

            migrationBuilder.DropTable(
                name: "RawMaterials");
        }
    }
}
