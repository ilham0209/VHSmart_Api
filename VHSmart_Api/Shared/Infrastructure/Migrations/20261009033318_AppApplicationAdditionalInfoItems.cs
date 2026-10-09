using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AppApplicationAdditionalInfoItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppApplicationAdditionalInfoItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Section = table.Column<int>(type: "int", maxLength: 20, nullable: false),
                    OptionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FreeText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppApplicationAdditionalInfoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppApplicationAdditionalInfoItems_AppHalalApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "AppHalalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppApplicationAdditionalInfoItems_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppApplicationAdditionalInfoItems_ApplicationId",
                table: "AppApplicationAdditionalInfoItems",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppApplicationAdditionalInfoItems_CompanyId",
                table: "AppApplicationAdditionalInfoItems",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppApplicationAdditionalInfoItems");
        }
    }
}
