using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CertificationBodyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BusinessRegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OwnerStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address3 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PostCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Telephone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Fax = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IndustrySize = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    DateOfEstablishment = table.Column<DateTime>(type: "date", nullable: true),
                    MainProductsServices = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Market = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComCompanies_AdmCertificationBodies_CertificationBodyId",
                        column: x => x.CertificationBodyId,
                        principalTable: "AdmCertificationBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComCompanies_AdmCountries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "AdmCountries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComCompanies_AdmSchemes_SchemeId",
                        column: x => x.SchemeId,
                        principalTable: "AdmSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComCompanyBrands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComCompanyBrands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComCompanyBrands_AdmGeneralData_BrandId",
                        column: x => x.BrandId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComCompanyBrands_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmRoles_CompanyId",
                table: "AdmRoles",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComCompanies_BusinessRegistrationNo",
                table: "ComCompanies",
                column: "BusinessRegistrationNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ComCompanies_CertificationBodyId",
                table: "ComCompanies",
                column: "CertificationBodyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComCompanies_CountryId",
                table: "ComCompanies",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_ComCompanies_Email",
                table: "ComCompanies",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ComCompanies_SchemeId",
                table: "ComCompanies",
                column: "SchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_ComCompanyBrands_BrandId",
                table: "ComCompanyBrands",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_ComCompanyBrands_CompanyId",
                table: "ComCompanyBrands",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComCompanyBrands_CompanyId_BrandId",
                table: "ComCompanyBrands",
                columns: new[] { "CompanyId", "BrandId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_AdmGeneralData_ComCompanies_CompanyId",
                table: "AdmGeneralData",
                column: "CompanyId",
                principalTable: "ComCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AdmRoles_ComCompanies_CompanyId",
                table: "AdmRoles",
                column: "CompanyId",
                principalTable: "ComCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AdmServiceProviders_ComCompanies_CompanyId",
                table: "AdmServiceProviders",
                column: "CompanyId",
                principalTable: "ComCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AdmSupportingDocuments_ComCompanies_CompanyId",
                table: "AdmSupportingDocuments",
                column: "CompanyId",
                principalTable: "ComCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AdmUserCompanies_ComCompanies_CompanyId",
                table: "AdmUserCompanies",
                column: "CompanyId",
                principalTable: "ComCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AdmWebLinks_ComCompanies_CompanyId",
                table: "AdmWebLinks",
                column: "CompanyId",
                principalTable: "ComCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdmGeneralData_ComCompanies_CompanyId",
                table: "AdmGeneralData");

            migrationBuilder.DropForeignKey(
                name: "FK_AdmRoles_ComCompanies_CompanyId",
                table: "AdmRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AdmServiceProviders_ComCompanies_CompanyId",
                table: "AdmServiceProviders");

            migrationBuilder.DropForeignKey(
                name: "FK_AdmSupportingDocuments_ComCompanies_CompanyId",
                table: "AdmSupportingDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AdmUserCompanies_ComCompanies_CompanyId",
                table: "AdmUserCompanies");

            migrationBuilder.DropForeignKey(
                name: "FK_AdmWebLinks_ComCompanies_CompanyId",
                table: "AdmWebLinks");

            migrationBuilder.DropTable(
                name: "ComCompanyBrands");

            migrationBuilder.DropTable(
                name: "ComCompanies");

            migrationBuilder.DropIndex(
                name: "IX_AdmRoles_CompanyId",
                table: "AdmRoles");
        }
    }
}
