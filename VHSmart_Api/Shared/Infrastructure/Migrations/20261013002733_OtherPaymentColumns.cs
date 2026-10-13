using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OtherPaymentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PaymentCategoryId",
                table: "PayPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceProviderId",
                table: "PayPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayPayments_PaymentCategoryId",
                table: "PayPayments",
                column: "PaymentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PayPayments_ServiceProviderId",
                table: "PayPayments",
                column: "ServiceProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_PayPayments_AdmGeneralData_PaymentCategoryId",
                table: "PayPayments",
                column: "PaymentCategoryId",
                principalTable: "AdmGeneralData",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayPayments_AdmServiceProviders_ServiceProviderId",
                table: "PayPayments",
                column: "ServiceProviderId",
                principalTable: "AdmServiceProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayPayments_AdmGeneralData_PaymentCategoryId",
                table: "PayPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_PayPayments_AdmServiceProviders_ServiceProviderId",
                table: "PayPayments");

            migrationBuilder.DropIndex(
                name: "IX_PayPayments_PaymentCategoryId",
                table: "PayPayments");

            migrationBuilder.DropIndex(
                name: "IX_PayPayments_ServiceProviderId",
                table: "PayPayments");

            migrationBuilder.DropColumn(
                name: "PaymentCategoryId",
                table: "PayPayments");

            migrationBuilder.DropColumn(
                name: "ServiceProviderId",
                table: "PayPayments");
        }
    }
}
