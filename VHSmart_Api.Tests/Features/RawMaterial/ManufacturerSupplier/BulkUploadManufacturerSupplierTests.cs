using FluentValidation;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.ManufacturerSupplier;
using VHSmart_Api.Shared.Domain.Admin;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.RawMaterial.ManufacturerSupplier;

// Bulk upload (spec 10.1 + 21.10): .xls/.xlsx, header row 4, max 250 data rows, partial
// success with one first-error-per-row list using physical sheet row numbers. One set of
// name/address/country cells feeds both halves - Type decides which half is stored (the
// Create texts, reused).
public class BulkUploadManufacturerSupplierTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();

    private static TestCurrentUser UserA() => new(Guid.NewGuid().ToString(), CompanyA);

    private static async Task<TestableVHSmartDbContext> CreateDbAsync(TestCurrentUser user)
    {
        var db = TestDbFactory.Create(TestDbFactory.NewDatabaseName(), user);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<Guid> MalaysiaIdAsync(TestableVHSmartDbContext db) =>
        await db.Countries
            .Where(country => country.IsoCode == "MYS")
            .Select(country => country.Id)
            .SingleAsync();

    // The template's CATEGORY column is the caller's own "Manufacturer Type" general data.
    private static async Task<Guid> SeedManufacturerTypeAsync(
        TestableVHSmartDbContext db,
        string name = "Food and Beverages")
    {
        var row = new GeneralDataEntity
        {
            CompanyId = CompanyA,
            Group = GeneralDataGroup.COMPANY,
            Category = "Manufacturer Type",
            Name = name
        };
        db.GeneralData.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static async Task<Guid> SeedRowAsync(
        TestableVHSmartDbContext db,
        ManufacturerSupplierType type,
        string? manufacturerEmail = null,
        string? supplierEmail = null)
    {
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = CompanyA,
            Type = type,
            ManufacturerName = type == ManufacturerSupplierType.SupplierOnly ? null : "Existing Manufacturer",
            ManufacturerAddress = type == ManufacturerSupplierType.SupplierOnly ? null : "Jalan Lama 1",
            ManufacturerCountryId = type == ManufacturerSupplierType.SupplierOnly
                ? null
                : await MalaysiaIdAsync(db),
            ManufacturerEmail = manufacturerEmail,
            SupplierName = type == ManufacturerSupplierType.ManufacturerOnly ? null : "Existing Supplier",
            SupplierAddress = type == ManufacturerSupplierType.ManufacturerOnly ? null : "Jalan Lama 2",
            SupplierCountryId = type == ManufacturerSupplierType.ManufacturerOnly
                ? null
                : await MalaysiaIdAsync(db),
            SupplierEmail = supplierEmail
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static BulkUploadManufacturerSuppliersCommand Command(
        byte[] bytes,
        string fileName = "manufacturers.xlsx") =>
        new(BulkUploadWorkbook.FormFile(bytes, fileName));

    private static Task<BulkUploadManufacturerSuppliersResponse> Upload(
        VHSmartDbContext db,
        TestCurrentUser user,
        byte[] bytes,
        string fileName = "manufacturers.xlsx") =>
        new BulkUploadManufacturerSuppliersHandler(db, user)
            .Handle(Command(bytes, fileName), CancellationToken.None);

    [Fact]
    public async Task Validator_NoFile_Fails()
    {
        var result = await new BulkUploadManufacturerSuppliersValidator()
            .ValidateAsync(new BulkUploadManufacturerSuppliersCommand(null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "File is required.");
    }

    [Fact]
    public async Task Handle_ValidRow_UploadsBothHalvesWithAuditStamp()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var typeId = await SeedManufacturerTypeAsync(db);

        var response = await Upload(db, user,
            BulkUploadManufacturerSupplierTestData.File(
                BulkUploadManufacturerSupplierTestData.Row()));

        Assert.Equal(1, response.TotalRows);
        Assert.Equal(1, response.UploadedRows);
        Assert.Empty(response.Errors);

        var stored = await db.ManufacturerSuppliers.AsNoTracking().SingleAsync();
        Assert.Equal(user.CompanyId, stored.CompanyId);
        Assert.Equal(user.UserId, stored.SysUserCreated);
        Assert.Equal(ManufacturerSupplierType.Both, stored.Type);
        Assert.Equal("Santan Foods Sdn Bhd", stored.ManufacturerName);
        Assert.Equal("Santan Foods Sdn Bhd", stored.SupplierName);
        Assert.Equal("Jalan Gombak 1", stored.ManufacturerAddress);
        Assert.Equal("Jalan Gombak 1", stored.SupplierAddress);
        Assert.Equal(await MalaysiaIdAsync(db), stored.ManufacturerCountryId);
        Assert.Equal(await MalaysiaIdAsync(db), stored.SupplierCountryId);
        Assert.Equal(typeId, stored.ManufacturerTypeId);
        Assert.Equal("hello@santan.example.com", stored.ManufacturerEmail);
        Assert.Equal("hello@santan.example.com", stored.SupplierEmail);
        Assert.Equal("202301001234", stored.ManufacturerBusinessRegNo);
        Assert.Equal("https://santan.example.com", stored.ManufacturerWebpage);
        Assert.Equal("Aminah", stored.SupplierPersonInCharge);
        Assert.Equal("0380000000", stored.SupplierContactNo);
    }

    [Fact]
    public async Task Handle_SupplierOnlyRow_ClearsManufacturerHalfAndSkipsCategory()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        // CATEGORY is only read for the manufacturer half (Database.md 8), so an unknown
        // value cannot fail a supplier-only row - the file marks the column mandatory.
        var response = await Upload(db, user,
            BulkUploadManufacturerSupplierTestData.File(
                BulkUploadManufacturerSupplierTestData.Row(
                    type: "Supplier", category: "No Such Type")));

        Assert.Equal(1, response.UploadedRows);
        Assert.Empty(response.Errors);

        var stored = await db.ManufacturerSuppliers.AsNoTracking().SingleAsync();
        Assert.Equal(ManufacturerSupplierType.SupplierOnly, stored.Type);
        Assert.Equal("Santan Foods Sdn Bhd", stored.SupplierName);
        Assert.Null(stored.ManufacturerName);
        Assert.Null(stored.ManufacturerBusinessRegNo);
        Assert.Null(stored.ManufacturerTypeId);
        Assert.Null(stored.ManufacturerEmail);
    }

    [Fact]
    public async Task Handle_PartialFailure_ReportsPhysicalRowNumbers()
    {
        // Sheet rows: 1-3 legend, 4 header, 5 valid, 6 duplicate e-mail, 7 unknown country.
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedManufacturerTypeAsync(db);

        var response = await Upload(db, user,
            BulkUploadManufacturerSupplierTestData.File(
                BulkUploadManufacturerSupplierTestData.Row(),
                BulkUploadManufacturerSupplierTestData.Row(name: "Second Foods"),
                BulkUploadManufacturerSupplierTestData.Row(name: "Third Foods", country: "Atlantis")));

        Assert.Equal(3, response.TotalRows);
        Assert.Equal(1, response.UploadedRows);
        Assert.Equal(2, response.Errors.Count);
        Assert.Equal(6, response.Errors[0].RowNumber);
        Assert.Equal(
            "A manufacturer with this e-mail already exists.",
            response.Errors[0].Message);
        Assert.Equal(7, response.Errors[1].RowNumber);
        Assert.Equal("Country not found.", response.Errors[1].Message);
        Assert.Single(await db.ManufacturerSuppliers.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Handle_DuplicateSupplierEmailAgainstExisting_FailsRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedManufacturerTypeAsync(db);
        await SeedRowAsync(
            db,
            ManufacturerSupplierType.Both,
            supplierEmail: "taken@santan.example.com");

        var response = await Upload(db, user,
            BulkUploadManufacturerSupplierTestData.File(
                BulkUploadManufacturerSupplierTestData.Row(
                    type: "Supplier", email: "taken@santan.example.com")));

        var error = Assert.Single(response.Errors);
        Assert.Equal(5, error.RowNumber);
        Assert.Equal("A supplier with this e-mail already exists.", error.Message);
        Assert.Equal(0, response.UploadedRows);
    }

    [Fact]
    public async Task Handle_UnknownCategory_FailsRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedManufacturerTypeAsync(db);

        var response = await Upload(db, user,
            BulkUploadManufacturerSupplierTestData.File(
                BulkUploadManufacturerSupplierTestData.Row(category: "No Such Type"),
                BulkUploadManufacturerSupplierTestData.Row(name: "Second Foods", category: "")));

        Assert.Equal(0, response.UploadedRows);
        Assert.Equal(2, response.Errors.Count);
        Assert.Equal(5, response.Errors[0].RowNumber);
        Assert.Equal("Manufacturer type not found.", response.Errors[0].Message);
        Assert.Equal(6, response.Errors[1].RowNumber);
        Assert.Equal("Manufacturer type is required.", response.Errors[1].Message);
    }

    [Fact]
    public async Task Handle_BlankOrInvalidType_FailsEachRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedManufacturerTypeAsync(db);

        var response = await Upload(db, user,
            BulkUploadManufacturerSupplierTestData.File(
                BulkUploadManufacturerSupplierTestData.Row(type: ""),
                BulkUploadManufacturerSupplierTestData.Row(type: "Warehouse")));

        Assert.Equal(0, response.UploadedRows);
        Assert.Equal(2, response.Errors.Count);
        Assert.Equal(5, response.Errors[0].RowNumber);
        Assert.Equal("Type is required.", response.Errors[0].Message);
        Assert.Equal(6, response.Errors[1].RowNumber);
        Assert.Equal("Type is invalid.", response.Errors[1].Message);
    }

    [Fact]
    public async Task Handle_NaAddress_ReadsAsBlank()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedManufacturerTypeAsync(db);

        // "N/A" is the template's "no data" placeholder (Q6), so it fails the Create rule
        // "Manufacturer address is required." instead of being stored as the address.
        var response = await Upload(db, user,
            BulkUploadManufacturerSupplierTestData.File(
                BulkUploadManufacturerSupplierTestData.Row(address: "N/A")));

        var error = Assert.Single(response.Errors);
        Assert.Equal(5, error.RowNumber);
        Assert.Equal("Manufacturer address is required.", error.Message);
        Assert.Empty(await db.ManufacturerSuppliers.ToListAsync());
    }

    [Fact]
    public async Task Handle_BlankRows_AreIgnored()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedManufacturerTypeAsync(db);

        var response = await Upload(db, user,
            BulkUploadManufacturerSupplierTestData.File(
                BulkUploadManufacturerSupplierTestData.Row(),
                [" "],
                [" "],
                BulkUploadManufacturerSupplierTestData.Row(name: "Second Foods", email: "second@santan.example.com")));

        Assert.Equal(2, response.TotalRows);
        Assert.Equal(2, response.UploadedRows);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public async Task Handle_NoDataRows_Throws()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, BulkUploadManufacturerSupplierTestData.File()));

        Assert.Equal("The file contains no data rows.", exception.Message);
    }

    [Fact]
    public async Task Handle_MoreThan250Rows_Throws()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedManufacturerTypeAsync(db);
        var rows = Enumerable.Range(0, 251)
            .Select(i => BulkUploadManufacturerSupplierTestData.Row(
                name: $"Foods {i}", email: $"bulk{i}@santan.example.com"))
            .ToArray();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, BulkUploadManufacturerSupplierTestData.File(rows)));

        Assert.Equal("The file exceeds the limit of 250 rows.", exception.Message);
        Assert.Empty(await db.ManufacturerSuppliers.ToListAsync());
    }

    [Fact]
    public async Task Handle_WrongExtension_Throws()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user,
                BulkUploadManufacturerSupplierTestData.File(
                    BulkUploadManufacturerSupplierTestData.Row()),
                "manufacturers.csv"));

        Assert.Equal("File type is not allowed. Allowed types: xls, xlsx.", exception.Message);
    }

    [Fact]
    public async Task Handle_CorruptBytes_Throws()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, [1, 2, 3, 4]));

        Assert.Equal("The file could not be read as an Excel workbook.", exception.Message);
    }

    [Fact]
    public async Task Handle_MissingRequiredColumn_Throws()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var headerWithoutCountry = BulkUploadManufacturerSupplierTestData.Header
            .Where(cell => !cell.StartsWith("COUNTRY", StringComparison.Ordinal))
            .ToArray();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, BulkUploadWorkbook.Sheet(
                [.. BulkUploadManufacturerSupplierTestData.Preamble, headerWithoutCountry,
                    BulkUploadManufacturerSupplierTestData.Row()])));

        Assert.Equal(
            "The file does not match the manufacturer and supplier bulk upload template.",
            exception.Message);
    }

    [Fact]
    public async Task Handle_NoTypeHeader_Throws()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, BulkUploadWorkbook.Sheet(
                [.. BulkUploadManufacturerSupplierTestData.Preamble,
                    ["COMPANY NAME", "ADDRESS"],
                    ["Santan Foods", "Jalan Gombak 1"]])));

        Assert.Equal(
            "The file does not match the manufacturer and supplier bulk upload template.",
            exception.Message);
    }
}
