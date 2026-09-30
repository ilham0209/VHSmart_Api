using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdmRolesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdmRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "bit", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CanView = table.Column<bool>(type: "bit", nullable: false),
                    CanCreate = table.Column<bool>(type: "bit", nullable: false),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
                    CanDelete = table.Column<bool>(type: "bit", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmRolePermissions_AdmRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AdmRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AdmRoles",
                columns: new[] { "Id", "CompanyId", "Description", "IsDeleted", "IsSystemRole", "Name", "SysDateCreated", "SysDateModified", "SysUserCreated", "SysUserModified" },
                values: new object[,]
                {
                    { new Guid("38ce3603-fddc-3cea-a130-16305f495c9e"), null, null, false, true, "Restaurant / Premise Manager", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), null, null, false, true, "VH Smart Admin", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), null, null, false, true, "Auditor / Chief Auditor", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null }
                });

            migrationBuilder.InsertData(
                table: "AdmRolePermissions",
                columns: new[] { "Id", "CanCreate", "CanDelete", "CanEdit", "CanView", "IsDeleted", "PermissionKey", "RoleId", "SysDateCreated", "SysDateModified", "SysUserCreated", "SysUserModified" },
                values: new object[,]
                {
                    { new Guid("01a6a1a4-30d2-be5f-9e69-bcbb0c8279fa"), true, true, true, true, false, "HalalApplication.CertificateItem", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("07ec2fe0-7ab7-43e5-bbb6-173326e549aa"), true, true, true, true, false, "Product.ManageProduct", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("08dbba6f-be39-f4d4-1805-ca180f66733f"), false, false, false, true, false, "Admin.GeneralData", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("0bda2516-e0f4-e60f-c170-9923ebca95d7"), true, true, true, true, false, "Audit.AuditPrefix", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("0d0b78cd-4cac-d3bd-f0e2-2dc98e11470f"), true, true, true, true, false, "Account.Setting", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("12c30412-1dd2-81d4-8776-43492d79dbc8"), true, true, true, true, false, "HalalApplication.HalalCertificate", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1f01c317-a6c5-6b5e-918e-b2ab90e2babd"), true, true, true, true, false, "Company.HalalPolicy", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("27808135-9417-2625-9032-7c11d4e596ff"), true, true, true, true, false, "Premise.ManagePremise", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("340c803b-149c-bf70-a83c-5abd1c0fe244"), true, true, true, true, false, "Payment.Certificate", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3507b5f8-671a-0c58-2d56-42da25d0f617"), false, false, false, true, false, "Premise.ManagePremise", new Guid("38ce3603-fddc-3cea-a130-16305f495c9e"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("39a56b38-7730-53ee-75f2-6572cdd067ac"), true, true, true, true, false, "Audit.AuditPrefix", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3b6f37c4-2deb-5cf2-4c96-2fa1dc4c1ec2"), true, true, true, true, false, "Audit.NonConformance", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("40884517-2c47-1be1-dd8d-6c582e27cbc2"), false, false, false, true, false, "Dashboard", new Guid("38ce3603-fddc-3cea-a130-16305f495c9e"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("418e7418-435f-026a-cc28-0375b418ef9a"), true, true, true, true, false, "Audit.AuditPlanning", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("43067f91-fc07-30b9-2ecd-d08a6c1e1270"), true, true, true, true, false, "Audit.AuditChecklist", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("452b7f47-054d-0b33-7b81-e8f1441c488c"), true, true, true, true, false, "RawMaterial.MasterList", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("48b31025-18c5-7b71-8f64-a5bb0f57488d"), true, true, true, true, false, "Product.ManageMenu", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4da02def-132b-ecdb-0220-526164cfd12e"), true, true, true, true, false, "Admin.ServiceProviders", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("57416391-5157-635b-3c53-f59b443986b4"), true, true, true, true, false, "Dashboard", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("59e3d80b-a777-3cb4-0dd4-6f64f33d91b2"), true, true, true, true, false, "HalalApplication.MyApplication", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("5f031a38-4069-bd83-1ecb-dc6af26eae10"), true, true, true, true, false, "Admin.WebLinks", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("669cbff2-c47f-7efc-fc46-4f378012a39c"), true, true, true, true, false, "Product.ManageMenuConcept", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("6d0f41b6-5189-08a9-95cd-8bc496c98d9c"), true, true, true, true, false, "Audit.Recommendation", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("6e85b8fd-f7cf-1bf5-5224-554b3331eed4"), false, false, true, true, false, "Audit.NonConformance", new Guid("38ce3603-fddc-3cea-a130-16305f495c9e"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("70f4b033-5cd1-68f8-981a-f99f3cb7a609"), true, true, true, true, false, "Audit.ExternalReport", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("71373fc5-d10f-f999-f08c-83d5b6f03753"), true, true, true, true, false, "Audit.GroupAuditor", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("72aa9565-41cd-fd52-9de5-e6d5840e26a6"), false, false, false, true, false, "Account.Setting", new Guid("38ce3603-fddc-3cea-a130-16305f495c9e"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("788aa22b-cbd7-8dfd-c90c-5729e93cb6b3"), true, true, true, true, false, "Admin.Companies", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("79c737bf-fc18-02ce-0599-16936eee1c79"), true, true, true, true, false, "Audit.Finding", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("79cc9636-9b91-0429-cd0a-b8abb0e8b6ac"), false, false, false, true, false, "Dashboard", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("7e118721-b35b-3785-a314-afe9c3432717"), false, false, false, true, false, "Account.Setting", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8056a50e-ece4-1ec8-21e7-f4fe56162f60"), true, true, true, true, false, "AdvancedSearch", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("89f74c93-9039-14c6-98fd-b620817c9293"), true, true, true, true, false, "Reference.View", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8af3d138-ff46-f043-56d3-a3e627a1fbbd"), true, true, true, true, false, "Company.Profiles", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8dca8f30-205c-4ed5-9ab7-56680843e2f5"), true, true, true, true, false, "Product.VerifyHalalProductUpdate", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("9093e27e-ec2c-18c0-6d15-91983ca70f34"), true, true, true, true, false, "Audit.AuditCriteria", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("96ce4195-b5dc-147a-e0fb-83c97eb43dce"), true, true, true, true, false, "HalalApplication.ManageBatch", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("98908295-f823-57e0-f31b-f513f5c45083"), true, true, true, true, false, "Company.InternalHalalCommittee", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("995994e3-4066-1a84-a2a4-c05ad828bd75"), true, true, true, true, false, "Audit.AuditPlanning", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("9cd85218-bf15-79c4-7649-9a4b0008d383"), true, true, true, true, false, "Audit.AuditTask", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("9f541486-2462-bd3b-3683-4f0696d8e6f3"), true, true, true, true, false, "Audit.NonConformance", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a5e2165b-c904-3939-9343-f6d724bf66c3"), true, true, true, true, false, "Personnel.AllStaff", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a75c63e5-c10f-4ec5-2e32-09e6950c3626"), true, true, true, true, false, "Audit.AuditCriteria", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("b9b7c460-2a01-2609-3792-106e7a8c4c04"), true, true, true, true, false, "Payment.Other", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c4005bf1-8bbc-45f2-bf8c-a0ec973644a9"), false, false, false, true, false, "Premise.ManagePremise", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c5608897-fdb1-f0ab-d032-e6f7b43c36b6"), true, true, true, true, false, "Support.Submit", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c78dda7e-9970-6cf3-10a4-1295942863ad"), true, true, true, true, false, "Audit.Finding", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c9f0bc1c-0317-3f8d-4f1f-d47b4f10cf2f"), true, true, true, true, false, "Admin.Users", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("cdf9f3b9-0188-af7c-569f-1132e3e8e801"), true, true, true, true, false, "Personnel.InternalTraining", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("d2a484e4-23d9-a768-5c24-fe68769f455a"), true, true, true, true, false, "Admin.GeneralData", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("ddc32abe-5489-4d64-85eb-4605ac18613d"), true, true, true, true, false, "Audit.GroupAuditor", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("e1bc766d-ebe5-bf79-e061-da9282aa5dc6"), true, true, true, true, false, "Admin.SupportingDocuments", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("e4ef5bde-f14c-9a3b-f1d7-e8ae1d4c1295"), true, true, true, true, false, "Audit.AuditTask", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f04be36d-63a7-1718-cd03-1e0384659c38"), true, true, true, true, false, "Admin.CertificationBodies", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f71ed060-778a-e5fd-7f98-5f7499508dd4"), true, true, true, true, false, "Audit.ExternalReport", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f7c120fc-84c8-aa0e-1b01-95db266e17f3"), true, true, true, true, false, "Audit.Recommendation", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f7e8aeb6-cf8a-c030-dbaf-19fb300aa9e1"), true, true, true, true, false, "RawMaterial.ManufacturerSupplier", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f83ca4e3-1269-471a-430e-7dc82125c4db"), true, true, true, true, false, "Audit.AuditChecklist", new Guid("c0603c53-253e-4d16-55fc-b2fac9cc3be3"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f8e525ad-07af-a24e-683c-b464ecaee789"), true, true, true, true, false, "Company.General", new Guid("9541c127-bd5a-0859-35e6-e00fc02e6ab2"), new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmRolePermissions_RoleId_PermissionKey",
                table: "AdmRolePermissions",
                columns: new[] { "RoleId", "PermissionKey" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmRolePermissions");

            migrationBuilder.DropTable(
                name: "AdmRoles");
        }
    }
}
