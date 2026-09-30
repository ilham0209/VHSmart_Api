using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VHSmart_Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CountriesStatesSchemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdmCountries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsoCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmCountries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmSchemes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsFoodPremise = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmSchemes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysUserCreated = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SysDateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SysUserModified = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SysDateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmStates_AdmCountries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "AdmCountries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AdmCountries",
                columns: new[] { "Id", "IsDeleted", "IsoCode", "Name", "SysDateCreated", "SysDateModified", "SysUserCreated", "SysUserModified" },
                values: new object[,]
                {
                    { new Guid("033cbd66-1774-2f9c-b9bf-2de0d8c0de19"), false, "COD", "Congo, Democratic Republic of the", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("03d0d19a-3521-2eb4-b866-0741d551859a"), false, "TTO", "Trinidad and Tobago", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("03f4e3d6-833b-6fbc-75cd-c69ffb33db1b"), false, "COK", "Cook Islands", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("04241211-766c-7794-39c2-8ce650f38f00"), false, "GRC", "Greece", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("054dafd5-f499-ebed-0210-0ec7d8bdedfd"), false, "VUT", "Vanuatu", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("057eb363-9dc3-d3d5-8834-5d629c026bdc"), false, "PRY", "Paraguay", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("0722cb5b-a4b7-9efa-4c40-2b0782e0182a"), false, "PLW", "Palau", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("07660392-2231-d9e1-023d-1cde106c60e1"), false, "GTM", "Guatemala", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("0798a984-f3ae-23c9-31c5-edecd8084186"), false, "ITA", "Italy", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("0a7504cf-7d2a-1f31-59ac-81ae9f8444cd"), false, "PHL", "Philippines", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("0d1cb51b-a04e-3a49-8abe-c8942f2907bd"), false, "ALB", "Albania", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("0e0f36c2-2ae9-38c8-5b15-6eb4ebc6c794"), false, "FRA", "France", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("0e3a56d3-2389-c83b-dd08-a70376a8ac1c"), false, "LKA", "Sri Lanka", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("11dadf0f-cc6e-7672-d8e7-a80731b4f2b6"), false, "SHN", "Saint Helena, Ascension and Tristan da Cunha", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("17055914-c043-8223-aae0-fe3f15d28978"), false, "CAF", "Central African Republic", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("17c2dbcb-e290-a8b1-d105-38e3faaf6600"), false, "BRA", "Brazil", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("18fda23a-89d9-ddc5-fe08-d60839aad1d9"), false, "AZE", "Azerbaijan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("19586824-14f6-1800-d6a3-6f0ed6573fd0"), false, "SXM", "Sint Maarten (Dutch part)", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("19d1c172-28c1-54dd-9915-9947ffc6102d"), false, "MLI", "Mali", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1a1a9f17-bb20-e7ac-9b2d-b9d9177727b5"), false, "IRL", "Ireland", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1aabd165-2e11-a101-5857-ac3a865e06c0"), false, "MNG", "Mongolia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1ac11b41-843e-c9d0-e9a7-aa38b4b31ecb"), false, "BGD", "Bangladesh", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1b2ae226-35a5-0dea-097c-e707e343422f"), false, "MAC", "Macao", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1b33a95c-631e-c16d-4cf3-a069f9356a3b"), false, "PRT", "Portugal", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1c078e89-7beb-b29d-d5d8-e585038a76bc"), false, "VAT", "Holy See", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1c6614fc-7199-4ef2-a327-0f26f91fbca1"), false, "BRN", "Brunei Darussalam", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1ce2463a-ba59-9b51-085c-96463b2d9f4e"), false, "DJI", "Djibouti", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1d841f33-a5f7-6cd1-434f-23ad7edfcf97"), false, "PRI", "Puerto Rico", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1ed88e8f-87bc-c2e3-a73c-c7a6993bc5a0"), false, "CHE", "Switzerland", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1f4222e8-56cb-215e-4b0c-c57b855f3a94"), false, "GNB", "Guinea-Bissau", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("1fa6ec81-0253-cd35-61b9-dba4371a32e0"), false, "BRB", "Barbados", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("20dfdc27-b2da-0303-4398-e204a2e49eee"), false, "BHR", "Bahrain", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("2225ad9b-df4c-b4d6-a646-59aa1af07dc3"), false, "TUR", "Türkiye", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("22fc250a-5521-3a72-8fb1-be455cf79e60"), false, "PCN", "Pitcairn", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("25fe55c3-fada-1d99-3d85-da45fdf527a0"), false, "MOZ", "Mozambique", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("27443c30-e5b4-66aa-a602-c173518cb54c"), false, "AIA", "Anguilla", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("28ba7824-b50d-8090-3852-c0c58ab144fd"), false, "GMB", "Gambia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("2909974e-969b-93fb-432f-e95866223ec4"), false, "MLT", "Malta", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("2b41099a-2c61-40c0-8108-540109280de3"), false, "GNQ", "Equatorial Guinea", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("2c9bdc98-7a38-77da-174d-b9d0943aa6ed"), false, "ERI", "Eritrea", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("2eacea49-ae41-6ba8-4036-cb341a1e89cd"), false, "CYP", "Cyprus", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("30d4e9c8-8758-ba07-d29b-71c367d2b00b"), false, "PER", "Peru", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("30f418e4-c6df-7972-f8ad-bcd1903f7194"), false, "MDG", "Madagascar", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("336fe15d-d32c-b7fe-b516-4cbb2565934a"), false, "BLZ", "Belize", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("33814bba-0299-608a-fd7e-d57f6b2c9952"), false, "COL", "Colombia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("340a6aac-ebc3-5a32-ca94-b06986a66dc2"), false, "RWA", "Rwanda", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3474471b-d20f-83f8-5379-5446e43e8a03"), false, "SGP", "Singapore", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("34a04228-6826-7d3f-3904-1af3934225b5"), false, "NZL", "New Zealand", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("357c98eb-b8f5-e7d1-b944-d5843b15bee2"), false, "BTN", "Bhutan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("36eed8e0-4762-0cd8-f771-a70ebd5a63e1"), false, "PSE", "Palestine, State of", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3919fd59-9b72-b48a-42cd-fceb24cddd6f"), false, "CHL", "Chile", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3aa8c065-5511-ef72-db0b-e155ecf27389"), false, "USA", "United States of America", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3b1bfcff-4084-3210-8693-acbc505df571"), false, "TUV", "Tuvalu", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3c0118bb-093a-412b-a8fe-2a1f5f8e37f6"), false, "LCA", "Saint Lucia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3df6ac59-1a2e-cc8e-182c-45be55fd6874"), false, "SPM", "Saint Pierre and Miquelon", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3e1b2391-fe3d-7d23-ac28-06f4d5675cd3"), false, "HKG", "Hong Kong", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3e3eb289-f3ff-05b4-c66a-b4d8a83cceb4"), false, "SMR", "San Marino", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3ef031b4-cfa8-2c24-2154-d6f0decd103f"), false, "BES", "Bonaire, Sint Eustatius and Saba", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("407ed2fd-c501-468f-29ea-bf20159ea45d"), false, "CMR", "Cameroon", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("41147191-563d-aee1-06a2-a7d76e3dae4b"), false, "MNP", "Northern Mariana Islands", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("41d2fa8a-f8c8-39c9-52b2-cf237935f99f"), false, "ECU", "Ecuador", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("434daec6-f6e1-19cd-2a37-3a0eee413ab0"), false, "DOM", "Dominican Republic", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("436ec934-418d-8f23-6fcb-9e868f7c6026"), false, "NCL", "New Caledonia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("463b9fe0-504b-1ad4-0400-f5b1e4cf779c"), false, "ESH", "Western Sahara", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("46814275-2a95-78c9-0417-f151e1960f15"), false, "CCK", "Cocos (Keeling) Islands", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4715a7b7-4339-88e0-ede7-4e583a234062"), false, "GIN", "Guinea", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4852f267-c7ad-6bc4-ec56-f15674e1b1ed"), false, "GHA", "Ghana", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4a522f41-87ae-cde2-13e3-11bf850855cf"), false, "BEL", "Belgium", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4b9a62e4-e9c0-18fa-4bc2-ce53eaac9928"), false, "ESP", "Spain", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4bd434f8-826e-6d9f-a7df-28b37d6825de"), false, "MCO", "Monaco", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4cc831dd-ba88-6c8f-feb0-17566d384674"), false, "CXR", "Christmas Island", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4d13c04a-cb59-4c32-f18b-9c83b0180737"), false, "MKD", "North Macedonia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4d347e11-4f8c-8b20-6286-e861c869efe7"), false, "MAF", "Saint Martin (French part)", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4d39e248-4455-ba99-36df-1f8f3464edeb"), false, "GUF", "French Guiana", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4d5112dc-366f-e4d9-1d68-d2533130a2ca"), false, "MNE", "Montenegro", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4d56c1da-ada7-90ee-8d1f-891020631e9c"), false, "PNG", "Papua New Guinea", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("4ff0530f-d14a-3bcc-79b2-b5b0760af6e2"), false, "MRT", "Mauritania", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("507fa633-1145-e5bb-4dfa-714a82a1470f"), false, "MEX", "Mexico", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("53d21967-8dfa-c9fa-30ca-b771bc75f933"), false, "KAZ", "Kazakhstan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("570e3dcc-841c-1b64-37cd-8f859377fa19"), false, "NGA", "Nigeria", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("5dd93356-d8f7-af18-7709-c56dc501c42b"), false, "TKM", "Turkmenistan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("5fac84fd-9f3c-d9ab-2cd8-c0457e8388a5"), false, "LBR", "Liberia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("5fd47624-3602-3b8d-87f6-0e9889844502"), false, "TLS", "Timor-Leste", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("63a4a723-e21b-4d99-84c6-63a3b5dd3f53"), false, "FLK", "Falkland Islands (Malvinas)", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("6555ed84-4326-3c34-f2a8-8e1b1e6ded15"), false, "ARG", "Argentina", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("6578b79c-4ac9-bdbd-e390-6f6784fde46b"), false, "ATG", "Antigua and Barbuda", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("68d9a763-a57d-6d55-71ad-aa7e16d8f283"), false, "MYT", "Mayotte", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("69eab5db-8e10-c42a-9edf-0562cbfd67e3"), false, "AND", "Andorra", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("6c065c28-28e3-1313-e677-89d1df02b701"), false, "URY", "Uruguay", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("6c6c9b1f-8f32-83bf-3e25-8b272536297a"), false, "OMN", "Oman", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("6d37d649-458a-c8c8-4ce2-67c44cdf3108"), false, "IDN", "Indonesia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("6d5a4e9b-45ef-8fff-c7f8-2cce148458f3"), false, "SLE", "Sierra Leone", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("6e9faf69-ef1b-f0f4-3f57-6684aba9a212"), false, "IOT", "British Indian Ocean Territory", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("6eabb5c1-fbc2-76a3-c77c-dac0db25c754"), false, "DMA", "Dominica", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("6fc1b3b7-95f2-f100-1bb5-a063b53b5d93"), false, "SUR", "Suriname", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("701d58ae-d8c7-6ba5-6dc6-87207f35df98"), false, "CHN", "China", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("7188a343-85aa-1b95-fb33-1f9eb6e61c9e"), false, "HTI", "Haiti", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("72b3f6c0-9ab1-cb96-7c8c-dfe56e2d9737"), false, "EGY", "Egypt", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("7625aef6-3fdd-752e-46e2-95beb3e2d5ab"), false, "CIV", "Côte d'Ivoire", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("78edaf03-7e84-c81e-4a50-5b7668f97e19"), false, "NIC", "Nicaragua", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("7b2d1bb8-fee9-973a-8075-fd9c6621690d"), false, "ARE", "United Arab Emirates", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("7b9f1398-d38c-717c-8bfe-4950e149920f"), false, "LBY", "Libya", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("7feb3fc5-9826-673c-bd81-e8ca999163ff"), false, "GEO", "Georgia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("80cb40f0-b927-c2fe-f1bc-d02c9b2aa5d6"), false, "FSM", "Micronesia, Federated States of", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("817a323a-6bd7-2645-1068-78edf9b7645f"), false, "TZA", "Tanzania, United Republic of", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8253a828-1adc-e3c7-f927-e289168ce265"), false, "ALA", "Åland Islands", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("82d01434-411f-af50-71cf-ad4999a90e0a"), false, "QAT", "Qatar", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("84054b3c-1b0a-a4f0-9e6e-0369d07dfcf9"), false, "EST", "Estonia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("84354ea8-f7ed-91eb-0b1b-9d5a500ddf0b"), false, "DEU", "Germany", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("846b3cca-3bb4-c0a8-db82-c2e77d63a016"), false, "GUM", "Guam", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8486e162-c153-9604-bfdb-8914b5ca91d3"), false, "VGB", "Virgin Islands (British)", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("857e6a15-b106-ec45-42de-4a4e1890e747"), false, "ASM", "American Samoa", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("86c9fe67-2cb5-913b-bba5-6490270f00cd"), false, "BWA", "Botswana", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("86f644f2-7ec2-5334-c5c5-348e32633a6d"), false, "POL", "Poland", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8759b520-eb5b-a35f-bb35-8538ad2cdd74"), false, "NLD", "Netherlands, Kingdom of the", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("895d75f8-a431-c4e4-3a7e-c1afb37faf6f"), false, "SVN", "Slovenia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8a24ffda-ae4a-181a-f88a-780e3d3e2d44"), false, "AUT", "Austria", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8a866e01-7780-468a-3259-116d16932c7c"), false, "WSM", "Samoa", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8b6d85e0-f09e-1c66-a4a1-2d88fbbe08c3"), false, "SSD", "South Sudan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8bd816c3-283a-9ba7-8473-9d4e0489d420"), false, "BIH", "Bosnia and Herzegovina", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8bf8fae4-eae1-c70d-648c-384cfa5c6e2b"), false, "NFK", "Norfolk Island", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8cebd043-6c77-7c3f-7dc2-65802a00a232"), false, "UGA", "Uganda", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8e0ac04a-95aa-785a-4d0d-5d110fb9f35a"), false, "ZWE", "Zimbabwe", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8fc0aacf-cbac-7740-c65b-4c1cd9b2fdc1"), false, "THA", "Thailand", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("901dafbc-9af0-207f-7ff9-e4f074d7c7a2"), false, "CPV", "Cabo Verde", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("918363d5-7352-e1bc-3228-445693ade7ad"), false, "SYR", "Syrian Arab Republic", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("91aa90c9-4e42-bead-02fb-fc54fe3e488f"), false, "KIR", "Kiribati", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("92d6b1e4-9d82-dc4b-ae9b-c2f950c85584"), false, "NPL", "Nepal", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("93e85444-8f17-5a2c-d5b6-f3911f2c9d30"), false, "ARM", "Armenia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("9446476a-7884-4881-f2a0-dd869fcd3c28"), false, "CUB", "Cuba", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("95ba7f7c-7f80-5e88-7aca-db45b25874c1"), false, "DNK", "Denmark", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("9607b558-5468-376f-d4aa-42b80416411d"), false, "BFA", "Burkina Faso", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("98897362-298b-85b2-8624-6c502c5a4bd0"), false, "ZAF", "South Africa", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("989129c0-e16a-817f-9367-3abf79f0b389"), false, "JOR", "Jordan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("9a8d0bfd-a743-0392-bcbf-5d41fe48a261"), false, "TJK", "Tajikistan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("9da763ff-859b-ecd3-2479-4ec54be88b99"), false, "JAM", "Jamaica", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("9e3d315e-1131-9587-9a72-b326bdc9280f"), false, "WLF", "Wallis and Futuna", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("9fe7ec70-b742-619e-ce9f-8cba9128bd33"), false, "TCA", "Turks and Caicos Islands", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a046b989-45b3-5d82-5f23-bc80c471ea8e"), false, "MUS", "Mauritius", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a0531b1e-bcb4-30c8-43e6-5e9945e3f748"), false, "NOR", "Norway", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a15aff80-27d6-1bea-01ae-7eb16868f756"), false, "GGY", "Guernsey", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a2f427b9-bc1c-46d9-3043-73378b5ff4b0"), false, "FIN", "Finland", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a30bea05-08ee-72bf-f263-5cf52e08aca5"), false, "HRV", "Croatia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a437b7ba-016f-2c76-87ea-d635ae52548e"), false, "SJM", "Svalbard and Jan Mayen", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a66a24c4-ffdc-c92f-6a89-647e026b3878"), false, "NAM", "Namibia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a76bc422-36f7-5ede-dbb1-9c1c777fd743"), false, "CZE", "Czechia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a80e92f3-6519-3605-08a8-f1bb0f970531"), false, "COM", "Comoros", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a85c6c95-6110-3766-6c63-c5c444105fba"), false, "LIE", "Liechtenstein", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a97ae32d-517c-456a-b407-16170ef9be13"), false, "MMR", "Myanmar", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("a9e845b0-df89-a4f7-515e-f705bbd27465"), false, "IRN", "Iran, Islamic Republic of", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("abf15b26-156b-5e4d-511a-27a17fd60809"), false, "KGZ", "Kyrgyzstan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("ad26a30e-bcc0-4210-a823-3e0f6262000a"), false, "ETH", "Ethiopia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("aec445c2-f133-27fd-8a5f-542e4e4a6cca"), false, "KOR", "Korea, Republic of", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("aee66b10-ff30-5b6e-3f78-2de8da6c9408"), false, "UZB", "Uzbekistan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("aef2d8b8-9b84-3098-6bde-bdb87cd167f2"), false, "CUW", "Curaçao", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("b1024e7a-47a6-8fca-4682-d8597ced18af"), false, "TKL", "Tokelau", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("b1aed76c-4ac7-83fa-36c8-3580b9f03a73"), false, "VIR", "Virgin Islands (U.S.)", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("b24108df-4946-e672-fdda-ce1db210540a"), false, "FRO", "Faroe Islands", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("b2eeda94-50a2-c999-a68b-f7aa8372c2a8"), false, "REU", "Réunion", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("b2f481fb-9fb4-5112-7f50-8ea6f56a8753"), false, "ISL", "Iceland", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("b45dc824-c041-a7c2-1cce-469bb9d05cd0"), false, "MTQ", "Martinique", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("b5432189-609e-e442-9789-d4143780aedf"), false, "MAR", "Morocco", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("b55a6fc9-38ef-700f-6b13-c3cc93486fde"), false, "MWI", "Malawi", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("b792acbf-a450-3205-a3cf-8cec28dd7f62"), false, "TGO", "Togo", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("b8202a1a-e3e6-9511-6c08-b17b85ad55a0"), false, "SWZ", "Eswatini", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("b9605973-8c1c-b248-af1b-81a46cf9907f"), false, "SDN", "Sudan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("baf3749f-aa5d-9755-184b-18dc3d8c25cc"), false, "BHS", "Bahamas", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("bbd586a5-f088-6820-fc84-4b84ea22ebe7"), false, "HND", "Honduras", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("bca04764-110f-7982-b418-1c18ac3d60f5"), false, "COG", "Congo", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("bf9ba943-b95c-7f2a-ee75-bcbcc2b1e734"), false, "MSR", "Montserrat", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("bfa743ee-3570-647a-a067-57d271b54596"), false, "SYC", "Seychelles", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("bfc32dd1-05f6-6364-3ff1-24b731b2e348"), false, "KWT", "Kuwait", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c01e86ba-a5f5-4aa5-1087-0cead42ef188"), false, "HMD", "Heard Island and McDonald Islands", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c06cd848-e881-03f4-6d74-23e5227832c6"), false, "YEM", "Yemen", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c2b99c53-bfe2-6329-56d8-eb58c88d319c"), false, "BOL", "Bolivia, Plurinational State of", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c362254c-cd4b-1974-5d85-b5035a52c8d8"), false, "ATF", "French Southern Territories", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c366fdf4-aafc-4759-192c-6948d3926b70"), false, "VCT", "Saint Vincent and the Grenadines", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c3958f81-51f7-fce2-e4ea-7350da7b694b"), false, "ROU", "Romania", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c5991d73-3c83-c7ea-765b-8c6e2b140c4c"), false, "FJI", "Fiji", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c5a5a875-f981-0cb5-88a9-61149f523d8c"), false, "TON", "Tonga", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c70a119b-52dc-921b-d677-fecfbc7c7547"), false, "CRI", "Costa Rica", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c730de7f-50b8-a4c7-6ebf-6c8ac25684a8"), false, "KHM", "Cambodia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c7e590ef-7d49-4ffb-7f04-e843a0b298fa"), false, "SEN", "Senegal", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c930c63b-2f7d-72cb-820a-db3943d1b475"), false, "UMI", "United States Minor Outlying Islands", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c93cfe13-6f19-89e2-4ea5-14e61afc3ed8"), false, "TWN", "Taiwan, Province of China", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c967f640-f7e0-2c6a-e5fc-6d2649d38eb9"), false, "SAU", "Saudi Arabia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("c98ab459-e887-7b11-4087-0264fe35a96c"), false, "GRD", "Grenada", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("cd30e909-2749-4232-25db-5414b627a43e"), false, "IMN", "Isle of Man", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("cd7cfbd1-c351-47d6-a537-d8ed566cff8d"), false, "GIB", "Gibraltar", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("cdc7a6d3-c378-cbce-3f29-6293c6135509"), false, "SLB", "Solomon Islands", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("cea7e982-fa43-13bc-add7-627a1f6d9513"), false, "SVK", "Slovakia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("cef51c69-592d-204c-0a24-e65cc667b2aa"), false, "IND", "India", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("cf354f4d-8199-6a8d-25a4-3eba43c3f98f"), false, "HUN", "Hungary", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("d289447b-d6f4-779c-82ae-c507e19f9d4d"), false, "VNM", "Viet Nam", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("d3cc0bba-63e4-164f-254a-3a376b915666"), false, "AFG", "Afghanistan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("d484aef8-dd06-6e56-940a-7782fd6c7325"), false, "ISR", "Israel", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("d51c9fd8-ce88-d8ff-dc40-e593f8b60c6d"), false, "STP", "Sao Tome and Principe", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("d64d32b0-26bd-9e4b-676c-817e16b7aed2"), false, "ATA", "Antarctica", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("d7544b7a-8aee-8307-7de5-2db78ab206a2"), false, "LVA", "Latvia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("d86d6461-c732-494f-10aa-079b1fc5419b"), false, "PAK", "Pakistan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("d874ab9d-8039-c9dc-8f1d-1c60351098f7"), false, "DZA", "Algeria", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("d8d3768b-620c-1223-a3ae-12b341e86db4"), false, "TUN", "Tunisia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("d946553c-2e3d-2bfd-a608-daa171bf62c5"), false, "BDI", "Burundi", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("dd081cff-fe7a-a4a3-76c3-a3df68f810ef"), false, "PRK", "Korea, Democratic People's Republic of", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("dd906c03-4eea-bb25-e694-27891ec2f4b1"), false, "MHL", "Marshall Islands", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("dde0071e-d56a-e6bd-71d0-c70a68115f87"), false, "LSO", "Lesotho", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("de83c70c-07f2-1406-522c-e5c5fdc4a4ab"), false, "GAB", "Gabon", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("dee9ed3c-cf52-0483-6c7d-af752893716c"), false, "RUS", "Russian Federation", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("df7a8bc6-a709-876a-9eba-0aafc03d353a"), false, "ZMB", "Zambia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("df9aee20-7e59-b604-c1a5-bb14baaa4e15"), false, "AUS", "Australia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("dfc8ace7-13c0-9ed6-37c6-9c1669136d6e"), false, "JPN", "Japan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("dffc8243-a007-fe11-a08f-89ff11b973e9"), false, "LTU", "Lithuania", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("e1aa9765-f562-0bb9-01ab-6adc121c0a57"), false, "SOM", "Somalia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("e478b1d7-8c54-c1ef-2867-b7249f2b9ec8"), false, "SGS", "South Georgia and the South Sandwich Islands", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("e49363dd-d48d-ba27-ebc2-2481884a4c38"), false, "BLR", "Belarus", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("e549e2b4-37ce-725f-763b-24f64c803b66"), false, "NRU", "Nauru", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("e5893599-16eb-609e-3527-4d5e2e2e7d59"), false, "IRQ", "Iraq", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("e5ca8558-346f-cbb0-61f5-91b4cb37a271"), false, "JEY", "Jersey", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("e6410554-a799-9361-65dd-e47652c1cb57"), false, "GRL", "Greenland", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("e7877da1-6fc4-fda4-3adb-9cd5d91b1a3b"), false, "PYF", "French Polynesia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("ea719014-df02-22a2-1464-2f7b29cbc4b2"), false, "AGO", "Angola", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("eb2fd01e-0290-e106-8eec-6828313bbf5a"), false, "SWE", "Sweden", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "MYS", "Malaysia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("edecd5cd-feb7-8070-e35d-3fc076671fa5"), false, "TCD", "Chad", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("ee80db49-936c-7e91-4b24-6e00a4b620d3"), false, "BLM", "Saint Barthélemy", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("eefc3403-0f15-5e21-7e1e-3a34df3f1511"), false, "LBN", "Lebanon", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("ef69fbf6-fe44-270e-415e-fd3140e957c6"), false, "LUX", "Luxembourg", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("ef9b3509-4b72-0009-4bbb-8dea9e2bdea7"), false, "GUY", "Guyana", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f097ffbc-f0b5-152d-455e-b663ac5d4eea"), false, "ABW", "Aruba", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f131df76-a0d1-6fdd-38a0-0275c897cb5d"), false, "MDA", "Moldova, Republic of", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f1735fa9-5923-8303-a003-1d25e6b841ed"), false, "NER", "Niger", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f4179d4d-341e-6df0-94de-10fafaade6e7"), false, "PAN", "Panama", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f60d1d63-7096-cab9-2089-e68b5c5db51b"), false, "VEN", "Venezuela, Bolivarian Republic of", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f6413527-a582-6fe6-0913-a2f12e379b05"), false, "CYM", "Cayman Islands", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f6caa31e-e89f-c4fd-2514-9e205b20b560"), false, "KNA", "Saint Kitts and Nevis", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f78ea1c6-d96c-ec23-d4e8-5c7bf2c53180"), false, "BGR", "Bulgaria", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f80d68f2-09db-d561-e72b-21065e247641"), false, "MDV", "Maldives", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f86ac57e-7302-9370-8b6b-938d79a1bbff"), false, "GLP", "Guadeloupe", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f87e8ab0-fcb1-2885-15a5-bbcac87b593c"), false, "BEN", "Benin", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f893793f-b3e4-d391-616a-dbb2b2aa6e11"), false, "NIU", "Niue", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("f980dc54-7e28-37b3-c284-148aa2c92686"), false, "UKR", "Ukraine", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("fb6b282e-9542-778d-2e15-cffa186929a0"), false, "KEN", "Kenya", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("fc00c10d-ae22-831b-1d38-76a41d126361"), false, "CAN", "Canada", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("fd41b0ff-0e6e-f3e1-6df5-e70dbcf9dbc3"), false, "GBR", "United Kingdom of Great Britain and Northern Ireland", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("fdab7097-9f0f-33f3-176a-d78e3827bff7"), false, "BMU", "Bermuda", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("fdebc886-8280-65f9-1242-165736edb137"), false, "LAO", "Lao People's Democratic Republic", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("febab4bc-5777-fe7c-412e-1b6ac0c35ca2"), false, "SRB", "Serbia", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("feca17e6-601e-ae7c-12ae-4c36213ef22d"), false, "BVT", "Bouvet Island", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("ffca54a3-a133-5476-1b1c-3549a7ad7b60"), false, "SLV", "El Salvador", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null }
                });

            migrationBuilder.InsertData(
                table: "AdmSchemes",
                columns: new[] { "Id", "Code", "IsDeleted", "IsFoodPremise", "Name", "SortOrder", "SysDateCreated", "SysDateModified", "SysUserCreated", "SysUserModified" },
                values: new object[,]
                {
                    { new Guid("17f555d8-762f-4d02-6f44-de9a7ee99494"), "PM", false, true, "Food Premises", 2, new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3485e49d-0851-a6f6-0ac0-d276cbbb05d8"), "OEM", false, false, "Original Equipment Manufacturing (OEM)", 9, new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("5e684020-2afe-860a-7472-f9ef9f253aa8"), "PL", false, false, "Logistics", 6, new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("916760d4-538c-c882-4121-2e209d17f478"), "PR", false, false, "Food and Beverages / Supplement Product", 1, new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("ae108343-7549-4c93-6498-e9e4caadfef6"), "FM", false, false, "Pharmaceutical", 5, new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("dcde1a7e-b34e-3f1b-afb6-05af86bd31c3"), "KO", false, false, "Cosmetics, Makeup & Personal Care(s)", 7, new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("e478c3e0-a27a-a4f8-5503-7679c1a604f4"), "BG", false, false, "Consumer's Good", 4, new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("feecd314-2cb4-d710-80b2-da452c487a17"), null, false, false, "Abattoirs", 3, new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("ff8d8b20-d481-069d-2951-0c8a2ce6cf52"), "MD", false, false, "Medical Devices", 8, new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null }
                });

            migrationBuilder.InsertData(
                table: "AdmStates",
                columns: new[] { "Id", "CountryId", "IsDeleted", "Name", "SysDateCreated", "SysDateModified", "SysUserCreated", "SysUserModified" },
                values: new object[,]
                {
                    { new Guid("2ac210f0-fe08-9644-da5d-105fdd7b997c"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Terengganu", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("31c408b8-414d-4468-389c-e62298547a32"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Negeri Sembilan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3badb97b-1020-b7b6-349f-3d518512e0b3"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Kelantan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("3cd86001-de2e-8d22-2404-2a729468bdae"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Kedah", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("42e2988f-a1c4-7365-c4b4-0e87a2cff7c5"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Perak", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("481fad24-544d-8837-19de-6ecd0345d873"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Pahang", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("5c673b9a-7ac5-5a46-1675-598c0f119fa3"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Sabah", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("6582a826-6e18-cdd5-00ad-1c624085f511"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Melaka", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("84d8d47b-9d7e-d8b7-b3e5-e804a6ad514e"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Kuala Lumpur", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("88291255-e0be-0d20-5590-05df40591abd"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Putrajaya", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("8a401f74-78a6-0a06-63e3-c94873d2992b"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Selangor", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("9eee0445-1b3d-b201-ea01-5fb3a593c01f"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Pulau Pinang", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("ad5d9af3-5f50-65d6-47f5-04d871f24558"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Perlis", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("fb476b99-2e62-4a3c-ade1-d54e736ce6c2"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Sarawak", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("fbd144a6-ba88-278a-0a4c-2b7a041807f2"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Labuan", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null },
                    { new Guid("fe8c4804-a585-5e17-2200-82ba74294c11"), new Guid("ec0d097d-926f-62bd-e284-2e7e219372f7"), false, "Johor", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, "system", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmStates_CountryId",
                table: "AdmStates",
                column: "CountryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmSchemes");

            migrationBuilder.DropTable(
                name: "AdmStates");

            migrationBuilder.DropTable(
                name: "AdmCountries");
        }
    }
}
