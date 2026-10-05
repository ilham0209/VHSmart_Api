using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PrdProductIngredients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrdProductIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawMaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MappingStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrdProductIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrdProductIngredients_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdProductIngredients_PrdProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "PrdProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdProductIngredients_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrdProductIngredients_CompanyId",
                table: "PrdProductIngredients",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdProductIngredients_ProductId",
                table: "PrdProductIngredients",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdProductIngredients_ProductId_RawMaterialId",
                table: "PrdProductIngredients",
                columns: new[] { "ProductId", "RawMaterialId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PrdProductIngredients_RawMaterialId",
                table: "PrdProductIngredients",
                column: "RawMaterialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrdProductIngredients");
        }
    }
}
