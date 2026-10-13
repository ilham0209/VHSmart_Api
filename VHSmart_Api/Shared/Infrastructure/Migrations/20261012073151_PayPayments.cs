using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PayPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BankAccountNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SubmittedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceivedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentVoucherNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaymentType = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    HalalApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateHalalApplication = table.Column<DateOnly>(type: "date", nullable: true),
                    ChargeLetterDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayPayments_AppHalalApplications_HalalApplicationId",
                        column: x => x.HalalApplicationId,
                        principalTable: "AppHalalApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayPayments_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayPayments_ComStaff_ReceivedByStaffId",
                        column: x => x.ReceivedByStaffId,
                        principalTable: "ComStaff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayPayments_ComStaff_SubmittedByStaffId",
                        column: x => x.SubmittedByStaffId,
                        principalTable: "ComStaff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayPaymentAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_PayPaymentAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayPaymentAttachments_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayPaymentAttachments_PayPayments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "PayPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayPaymentAttachments_CompanyId",
                table: "PayPaymentAttachments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PayPaymentAttachments_PaymentId",
                table: "PayPaymentAttachments",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PayPayments_CompanyId",
                table: "PayPayments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PayPayments_HalalApplicationId",
                table: "PayPayments",
                column: "HalalApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_PayPayments_ReceivedByStaffId",
                table: "PayPayments",
                column: "ReceivedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PayPayments_SubmittedByStaffId",
                table: "PayPayments",
                column: "SubmittedByStaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayPaymentAttachments");

            migrationBuilder.DropTable(
                name: "PayPayments");
        }
    }
}
