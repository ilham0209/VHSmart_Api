using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AudFindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FindingCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudFindings_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AudFindingRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecommendationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudFindingRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudFindingRecommendations_AudFindings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "AudFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudFindingRecommendations_AudRecommendations_RecommendationId",
                        column: x => x.RecommendationId,
                        principalTable: "AudRecommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AudFindingRecommendations_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudFindingRecommendations_CompanyId",
                table: "AudFindingRecommendations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudFindingRecommendations_FindingId",
                table: "AudFindingRecommendations",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_AudFindingRecommendations_FindingId_RecommendationId",
                table: "AudFindingRecommendations",
                columns: new[] { "FindingId", "RecommendationId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AudFindingRecommendations_RecommendationId",
                table: "AudFindingRecommendations",
                column: "RecommendationId");

            migrationBuilder.CreateIndex(
                name: "IX_AudFindings_CompanyId",
                table: "AudFindings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AudFindings_CompanyId_FindingCode",
                table: "AudFindings",
                columns: new[] { "CompanyId", "FindingCode" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudFindingRecommendations");

            migrationBuilder.DropTable(
                name: "AudFindings");
        }
    }
}
