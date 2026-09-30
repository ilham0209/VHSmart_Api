using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UsersAndLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdmUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPlatformAdmin = table.Column<bool>(type: "bit", nullable: false),
                    ContactNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ProfilePicture_FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ProfilePicture_StorageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProfilePicture_ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProfilePicture_SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmUsers_AdmRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AdmRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdmUserCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmUserCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmUserCompanies_AdmUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AdmUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdmUserTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmUserTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmUserTokens_AdmUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AdmUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmUserCompanies_CompanyId",
                table: "AdmUserCompanies",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmUserCompanies_UserId_CompanyId",
                table: "AdmUserCompanies",
                columns: new[] { "UserId", "CompanyId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AdmUsers_Email",
                table: "AdmUsers",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AdmUsers_RoleId",
                table: "AdmUsers",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmUserTokens_TokenHash",
                table: "AdmUserTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_AdmUserTokens_UserId",
                table: "AdmUserTokens",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdmNotifications_AdmUsers_UserId",
                table: "AdmNotifications",
                column: "UserId",
                principalTable: "AdmUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdmNotifications_AdmUsers_UserId",
                table: "AdmNotifications");

            migrationBuilder.DropTable(
                name: "AdmUserCompanies");

            migrationBuilder.DropTable(
                name: "AdmUserTokens");

            migrationBuilder.DropTable(
                name: "AdmUsers");
        }
    }
}
