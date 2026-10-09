using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AppBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CbReferenceNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubmissionPlannedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManufacturerSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBatches_AdmGeneralData_BrandId",
                        column: x => x.BrandId,
                        principalTable: "AdmGeneralData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBatches_AdmSchemes_SchemeId",
                        column: x => x.SchemeId,
                        principalTable: "AdmSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBatches_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBatches_RawManufacturerSuppliers_ManufacturerSupplierId",
                        column: x => x.ManufacturerSupplierId,
                        principalTable: "RawManufacturerSuppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppBatchPremises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PremiseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBatchPremises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBatchPremises_AppBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "AppBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBatchPremises_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBatchPremises_ComPremises_PremiseId",
                        column: x => x.PremiseId,
                        principalTable: "ComPremises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppBatchProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MappingStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBatchProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBatchProducts_AppBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "AppBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBatchProducts_ComCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "ComCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBatchProducts_PrdProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "PrdProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppBatches_BrandId",
                table: "AppBatches",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBatches_CompanyId",
                table: "AppBatches",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBatches_CompanyId_Name",
                table: "AppBatches",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppBatches_ManufacturerSupplierId",
                table: "AppBatches",
                column: "ManufacturerSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBatches_SchemeId",
                table: "AppBatches",
                column: "SchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBatchPremises_BatchId",
                table: "AppBatchPremises",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBatchPremises_BatchId_PremiseId",
                table: "AppBatchPremises",
                columns: new[] { "BatchId", "PremiseId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppBatchPremises_CompanyId",
                table: "AppBatchPremises",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBatchPremises_PremiseId",
                table: "AppBatchPremises",
                column: "PremiseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBatchProducts_BatchId",
                table: "AppBatchProducts",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBatchProducts_BatchId_ProductId",
                table: "AppBatchProducts",
                columns: new[] { "BatchId", "ProductId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppBatchProducts_CompanyId",
                table: "AppBatchProducts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBatchProducts_ProductId",
                table: "AppBatchProducts",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppBatchPremises");

            migrationBuilder.DropTable(
                name: "AppBatchProducts");

            migrationBuilder.DropTable(
                name: "AppBatches");
        }
    }
}
