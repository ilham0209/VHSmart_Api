using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AppHalalApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppHalalApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ApplicationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    StatusDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CbApplicationNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CbApplicationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HalalCoachName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SurveyReadProcedureManual = table.Column<bool>(type: "bit", nullable: false),
                    SurveyReadMs1500 = table.Column<bool>(type: "bit", nullable: false),
                    SurveyHandlesProhibited = table.Column<bool>(type: "bit", nullable: false),
                    SurveyHasIhc = table.Column<bool>(type: "bit", nullable: false),
                    YearlySalesRevenue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProductMarket = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    WorkingHourFrom = table.Column<TimeOnly>(type: "time", nullable: true),
                    WorkingHourTo = table.Column<TimeOnly>(type: "time", nullable: true),
                    NumberOfShifts = table.Column<int>(type: "int", nullable: true),
                    MuslimManagement = table.Column<int>(type: "int", nullable: true),
                    MuslimFoodHandler = table.Column<int>(type: "int", nullable: true),
                    MuslimChef = table.Column<int>(type: "int", nullable: true),
                    NonMuslimManagement = table.Column<int>(type: "int", nullable: true),
                    NonMuslimFoodHandler = table.Column<int>(type: "int", nullable: true),
                    NonMuslimChef = table.Column<int>(type: "int", nullable: true),
                    AckName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AckEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    AckMobile = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AckAccepted = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppHalalApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppHalalApplications_AdmSchemes_SchemeId",
                        column: x => x.SchemeId,
                        principalTable: "AdmSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppHalalApplications_AppBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "AppBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppHalalApplications_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppApplicationStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppApplicationStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppApplicationStatusHistories_AppHalalApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "AppHalalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppApplicationStatusHistories_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppApplicationStatusHistories_ApplicationId",
                table: "AppApplicationStatusHistories",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppApplicationStatusHistories_CompanyId",
                table: "AppApplicationStatusHistories",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppHalalApplications_BatchId",
                table: "AppHalalApplications",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AppHalalApplications_CompanyId",
                table: "AppHalalApplications",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppHalalApplications_ReferenceNo",
                table: "AppHalalApplications",
                column: "ReferenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppHalalApplications_SchemeId",
                table: "AppHalalApplications",
                column: "SchemeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppApplicationStatusHistories");

            migrationBuilder.DropTable(
                name: "AppHalalApplications");
        }
    }
}
