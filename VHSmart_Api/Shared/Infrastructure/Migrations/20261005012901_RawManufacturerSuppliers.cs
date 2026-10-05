using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RawManufacturerSuppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RawManufacturerSuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ManufacturerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ManufacturerBusinessRegNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ManufacturerTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManufacturerAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ManufacturerCountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManufacturerPersonInCharge = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ManufacturerContactNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ManufacturerEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    ManufacturerWebpage = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupplierAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupplierCountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierPersonInCharge = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupplierContactNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SupplierEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Logo_FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Logo_StorageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Logo_ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Logo_SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawManufacturerSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawManufacturerSuppliers_AdmCountries_ManufacturerCountryId",
                        column: x => x.ManufacturerCountryId,
                        principalTable: "AdmCountries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawManufacturerSuppliers_AdmCountries_SupplierCountryId",
                        column: x => x.SupplierCountryId,
                        principalTable: "AdmCountries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawManufacturerSuppliers_AdmGeneralData_ManufacturerTypeId",
                        column: x => x.ManufacturerTypeId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawManufacturerSuppliers_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RawManufacturerSuppliers_CompanyId",
                table: "RawManufacturerSuppliers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_RawManufacturerSuppliers_CompanyId_ManufacturerEmail",
                table: "RawManufacturerSuppliers",
                columns: new[] { "CompanyId", "ManufacturerEmail" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [ManufacturerEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RawManufacturerSuppliers_CompanyId_SupplierEmail",
                table: "RawManufacturerSuppliers",
                columns: new[] { "CompanyId", "SupplierEmail" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [SupplierEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RawManufacturerSuppliers_ManufacturerCountryId",
                table: "RawManufacturerSuppliers",
                column: "ManufacturerCountryId");

            migrationBuilder.CreateIndex(
                name: "IX_RawManufacturerSuppliers_ManufacturerTypeId",
                table: "RawManufacturerSuppliers",
                column: "ManufacturerTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RawManufacturerSuppliers_SupplierCountryId",
                table: "RawManufacturerSuppliers",
                column: "SupplierCountryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawManufacturerSuppliers");
        }
    }
}
