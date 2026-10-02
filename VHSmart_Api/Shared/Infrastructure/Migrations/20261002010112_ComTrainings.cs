using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComTrainings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComTrainings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainingType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TrainingDate = table.Column<DateTime>(type: "date", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComTrainings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComTrainings_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComTrainingAttendees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComTrainingAttendees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComTrainingAttendees_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComTrainingAttendees_ComStaff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "ComStaff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComTrainingAttendees_ComTrainings_TrainingId",
                        column: x => x.TrainingId,
                        principalTable: "ComTrainings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComTrainingModules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_ComTrainingModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComTrainingModules_AdmGeneralData_ModuleTypeId",
                        column: x => x.ModuleTypeId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComTrainingModules_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComTrainingModules_ComTrainings_TrainingId",
                        column: x => x.TrainingId,
                        principalTable: "ComTrainings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComTrainingAttendees_CompanyId",
                table: "ComTrainingAttendees",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComTrainingAttendees_StaffId",
                table: "ComTrainingAttendees",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ComTrainingAttendees_TrainingId_StaffId",
                table: "ComTrainingAttendees",
                columns: new[] { "TrainingId", "StaffId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ComTrainingModules_CompanyId",
                table: "ComTrainingModules",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComTrainingModules_ModuleTypeId",
                table: "ComTrainingModules",
                column: "ModuleTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ComTrainingModules_TrainingId",
                table: "ComTrainingModules",
                column: "TrainingId");

            migrationBuilder.CreateIndex(
                name: "IX_ComTrainings_CompanyId",
                table: "ComTrainings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ComTrainings_CompanyId_Name",
                table: "ComTrainings",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComTrainingAttendees");

            migrationBuilder.DropTable(
                name: "ComTrainingModules");

            migrationBuilder.DropTable(
                name: "ComTrainings");
        }
    }
}
