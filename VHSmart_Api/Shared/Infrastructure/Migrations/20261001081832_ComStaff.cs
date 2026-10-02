using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComStaff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    TitleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IdType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IdNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmployeeIdNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Religion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DesignationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfficeNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    HasTyphoidInjection = table.Column<bool>(type: "bit", nullable: false),
                    TyphoidExpiryDate = table.Column<DateTime>(type: "date", nullable: true),
                    IsIhcMember = table.Column<bool>(type: "bit", nullable: false),
                    IhcRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Photo_FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Photo_StorageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Photo_ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Photo_SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComStaff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComStaff_AdmGeneralData_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComStaff_AdmGeneralData_DesignationId",
                        column: x => x.DesignationId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComStaff_AdmGeneralData_IhcRoleId",
                        column: x => x.IhcRoleId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComStaff_AdmGeneralData_TitleId",
                        column: x => x.TitleId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComStaff_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComStaffAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Document_FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Document_StorageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Document_ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Document_SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComStaffAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComStaffAttachments_AdmSupportingDocuments_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "AdmSupportingDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComStaffAttachments_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComStaffAttachments_ComStaff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "ComStaff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComStaff_CompanyId",
                table: "ComStaff",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComStaff_CompanyId_Email",
                table: "ComStaff",
                columns: new[] { "CompanyId", "Email" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ComStaff_DepartmentId",
                table: "ComStaff",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComStaff_DesignationId",
                table: "ComStaff",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_ComStaff_IhcRoleId",
                table: "ComStaff",
                column: "IhcRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ComStaff_TitleId",
                table: "ComStaff",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_ComStaffAttachments_CompanyId",
                table: "ComStaffAttachments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComStaffAttachments_DocumentTypeId",
                table: "ComStaffAttachments",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ComStaffAttachments_StaffId",
                table: "ComStaffAttachments",
                column: "StaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComStaffAttachments");

            migrationBuilder.DropTable(
                name: "ComStaff");
        }
    }
}
