using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PrdMenuConcepts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrdMenuConcepts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrdMenuConcepts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrdMenuConcepts_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrdMenuConceptMenus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuConceptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MappingStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrdMenuConceptMenus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrdMenuConceptMenus_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdMenuConceptMenus_PrdMenuConcepts_MenuConceptId",
                        column: x => x.MenuConceptId,
                        principalTable: "PrdMenuConcepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrdMenuConceptMenus_PrdMenus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "PrdMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComPremises_MenuConceptId",
                table: "ComPremises",
                column: "MenuConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenuConceptMenus_CompanyId",
                table: "PrdMenuConceptMenus",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenuConceptMenus_MenuConceptId",
                table: "PrdMenuConceptMenus",
                column: "MenuConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenuConceptMenus_MenuConceptId_MenuId",
                table: "PrdMenuConceptMenus",
                columns: new[] { "MenuConceptId", "MenuId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenuConceptMenus_MenuId",
                table: "PrdMenuConceptMenus",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_PrdMenuConcepts_CompanyId",
                table: "PrdMenuConcepts",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_ComPremises_PrdMenuConcepts_MenuConceptId",
                table: "ComPremises",
                column: "MenuConceptId",
                principalTable: "PrdMenuConcepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ComPremises_PrdMenuConcepts_MenuConceptId",
                table: "ComPremises");

            migrationBuilder.DropTable(
                name: "PrdMenuConceptMenus");

            migrationBuilder.DropTable(
                name: "PrdMenuConcepts");

            migrationBuilder.DropIndex(
                name: "IX_ComPremises_MenuConceptId",
                table: "ComPremises");
        }
    }
}
