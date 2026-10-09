using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppHalalCertificatesAppCertificateItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppHalalCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IssuedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Document_FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Document_StorageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Document_ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Document_SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppHalalCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppHalalCertificates_AppHalalApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "AppHalalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppHalalCertificates_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppCertificateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PremiseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HalalCertificateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppCertificateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppCertificateItems_AdmGeneralData_BrandId",
                        column: x => x.BrandId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppCertificateItems_AppHalalApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "AppHalalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppCertificateItems_AppHalalCertificates_HalalCertificateId",
                        column: x => x.HalalCertificateId,
                        principalTable: "AppHalalCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppCertificateItems_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppCertificateItems_ComPremises_PremiseId",
                        column: x => x.PremiseId,
                        principalTable: "ComPremises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppCertificateItems_PrdProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "PrdProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateItems_ApplicationId",
                table: "AppCertificateItems",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateItems_BrandId",
                table: "AppCertificateItems",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateItems_CompanyId",
                table: "AppCertificateItems",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateItems_HalalCertificateId",
                table: "AppCertificateItems",
                column: "HalalCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateItems_PremiseId",
                table: "AppCertificateItems",
                column: "PremiseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateItems_ProductId",
                table: "AppCertificateItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AppHalalCertificates_ApplicationId",
                table: "AppHalalCertificates",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppHalalCertificates_CompanyId",
                table: "AppHalalCertificates",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppHalalCertificates_CompanyId_CertificateNo",
                table: "AppHalalCertificates",
                columns: new[] { "CompanyId", "CertificateNo" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCertificateItems");

            migrationBuilder.DropTable(
                name: "AppHalalCertificates");
        }
    }
}
