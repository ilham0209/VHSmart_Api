using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PrdMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrdMenus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: true),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrdMenus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrdMenus_AdmGeneralData_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdMenus_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrdMenuAccessibleCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessibleCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrdMenuAccessibleCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrdMenuAccessibleCompanies_ComCompanies_AccessibleCompanyId",
                        column: x => x.AccessibleCompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdMenuAccessibleCompanies_PrdMenus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "PrdMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrdMenuRawMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawMaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrdMenuRawMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrdMenuRawMaterials_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdMenuRawMaterials_PrdMenus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "PrdMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdMenuRawMaterials_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenuAccessibleCompanies_AccessibleCompanyId",
                table: "PrdMenuAccessibleCompanies",
                column: "AccessibleCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenuAccessibleCompanies_MenuId",
                table: "PrdMenuAccessibleCompanies",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenuRawMaterials_CompanyId",
                table: "PrdMenuRawMaterials",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenuRawMaterials_MenuId",
                table: "PrdMenuRawMaterials",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenuRawMaterials_MenuId_RawMaterialId",
                table: "PrdMenuRawMaterials",
                columns: new[] { "MenuId", "RawMaterialId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenuRawMaterials_RawMaterialId",
                table: "PrdMenuRawMaterials",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenus_CategoryId",
                table: "PrdMenus",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenus_CompanyId",
                table: "PrdMenus",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenus_CompanyId_CategoryId_Name",
                table: "PrdMenus",
                columns: new[] { "CompanyId", "CategoryId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrdMenuAccessibleCompanies");

            migrationBuilder.DropTable(
                name: "PrdMenuRawMaterials");

            migrationBuilder.DropTable(
                name: "PrdMenus");
        }
    }
}
