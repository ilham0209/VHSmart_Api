using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComPremises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComPremises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PremiseType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    StoreCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MenuConceptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PremiseManagerStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PremiseManagerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AreaManagerStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationManagerStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessRegistrationNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GoogleMapLink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Address1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address3 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Telephone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Fax = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    OpeningDate = table.Column<DateTime>(type: "date", nullable: true),
                    ClosingDate = table.Column<DateTime>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PrayerRoomAvailabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComPremises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComPremises_AdmCountries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "AdmCountries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComPremises_AdmGeneralData_BrandId",
                        column: x => x.BrandId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComPremises_AdmGeneralData_PrayerRoomAvailabilityId",
                        column: x => x.PrayerRoomAvailabilityId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComPremises_AdmGeneralData_TagId",
                        column: x => x.TagId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComPremises_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComPremises_ComStaff_AreaManagerStaffId",
                        column: x => x.AreaManagerStaffId,
                        principalTable: "ComStaff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComPremises_ComStaff_OperationManagerStaffId",
                        column: x => x.OperationManagerStaffId,
                        principalTable: "ComStaff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComPremises_ComStaff_PremiseManagerStaffId",
                        column: x => x.PremiseManagerStaffId,
                        principalTable: "ComStaff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComPremiseContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PremiseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComPremiseContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComPremiseContacts_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComPremiseContacts_ComPremises_PremiseId",
                        column: x => x.PremiseId,
                        principalTable: "ComPremises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComPremiseContacts_ComStaff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "ComStaff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComPremiseHostels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PremiseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenancyExpiryDate = table.Column<DateTime>(type: "date", nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PhoneNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComPremiseHostels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComPremiseHostels_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComPremiseHostels_ComPremises_PremiseId",
                        column: x => x.PremiseId,
                        principalTable: "ComPremises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComPremiseContacts_CompanyId",
                table: "ComPremiseContacts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremiseContacts_PremiseId_StaffId",
                table: "ComPremiseContacts",
                columns: new[] { "PremiseId", "StaffId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremiseContacts_StaffId",
                table: "ComPremiseContacts",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremiseHostels_CompanyId",
                table: "ComPremiseHostels",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremiseHostels_PremiseId",
                table: "ComPremiseHostels",
                column: "PremiseId");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremises_AreaManagerStaffId",
                table: "ComPremises",
                column: "AreaManagerStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremises_BrandId",
                table: "ComPremises",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremises_CompanyId",
                table: "ComPremises",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremises_CompanyId_Email",
                table: "ComPremises",
                columns: new[] { "CompanyId", "Email" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremises_CompanyId_StoreCode",
                table: "ComPremises",
                columns: new[] { "CompanyId", "StoreCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremises_CountryId",
                table: "ComPremises",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremises_OperationManagerStaffId",
                table: "ComPremises",
                column: "OperationManagerStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremises_PrayerRoomAvailabilityId",
                table: "ComPremises",
                column: "PrayerRoomAvailabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremises_PremiseManagerStaffId",
                table: "ComPremises",
                column: "PremiseManagerStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ComPremises_TagId",
                table: "ComPremises",
                column: "TagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComPremiseContacts");

            migrationBuilder.DropTable(
                name: "ComPremiseHostels");

            migrationBuilder.DropTable(
                name: "ComPremises");
        }
    }
}
