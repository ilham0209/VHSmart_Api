using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Subscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdmSubscriptionPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: true),
                    MaxPremises = table.Column<int>(type: "int", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmSubscriptionPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmCompanySubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DurationMonths = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    ExpiryWarningSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmCompanySubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmCompanySubscriptions_AdmSubscriptionPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "AdmSubscriptionPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmCompanySubscriptions_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AdmSubscriptionPackages",
                columns: new[] { "Id", "Code", "IsDeleted", "MaxPremises", "MaxUsers", "Name", "SysDateCreated", "SysDateModified", "SysUserCreated", "SysUserModified" },
                values: new object[,]
                {
                    { new Guid("707493bc-cb53-f48f-42f3-1704448ec9d6"), "BSC-MICRO", false, null, null, "Basic Micro", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("7ef8c53d-ec5f-73bb-33f0-97f127a99c3f"), "ADC", false, 5, 10, "Advanced", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("89bcf2d9-cb0e-6355-c25b-d65c7271d408"), "TRL", false, null, null, "Trial", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("968a7d73-0ca4-488a-8d08-8be235a51123"), "LTE", false, 1, 1, "Lite", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("d450231d-5b75-b973-a3ab-0c1744ba964d"), "EASY-HOME", false, null, null, "Easy Home", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("ee5b32c4-cd73-1f79-55bd-31fc664430e7"), "PLA", false, null, null, "Premium", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmCompanySubscriptions_CompanyId",
                table: "AdmCompanySubscriptions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmCompanySubscriptions_PackageId",
                table: "AdmCompanySubscriptions",
                column: "PackageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmCompanySubscriptions");

            migrationBuilder.DropTable(
                name: "AdmSubscriptionPackages");
        }
    }
}
