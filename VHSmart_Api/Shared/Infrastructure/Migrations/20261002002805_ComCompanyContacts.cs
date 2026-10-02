using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComCompanyContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumberOfEmployees",
                table: "ComCompanies",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ComCompanyContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkingHourFrom = table.Column<TimeOnly>(type: "time", nullable: true),
                    WorkingHourTo = table.Column<TimeOnly>(type: "time", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComCompanyContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComCompanyContacts_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComCompanyContacts_ComStaff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "ComStaff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComCompanyContacts_CompanyId",
                table: "ComCompanyContacts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComCompanyContacts_CompanyId_Kind_StaffId",
                table: "ComCompanyContacts",
                columns: new[] { "CompanyId", "Kind", "StaffId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ComCompanyContacts_StaffId",
                table: "ComCompanyContacts",
                column: "StaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComCompanyContacts");

            migrationBuilder.DropColumn(
                name: "NumberOfEmployees",
                table: "ComCompanies");
        }
    }
}
