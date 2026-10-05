using FluentValidation;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.RawMaterial.MasterList;
using VHSmart_Api.Shared.Domain.RawMaterial;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.RawMaterial.MasterList;

// Bulk upload (spec 10.2 + 21.10): .xls/.xlsx, header row 4, max 250 data rows, partial
// success with one first-error-per-row list using physical sheet row numbers. The two
// template blocks link an existing manufacturer & supplier BY E-MAIL or build a new row (Q16).
public class BulkUploadRawMaterialTests
{
    private static async Task<(Guid StatusId, Guid SourceId)> SeedDropdownsAsync(
        TestableVHSmartDbContext db) =>
        (
            await RawMaterialTestData.SeedGeneralDataAsync(db, "Ingredient Status", "Active"),
            await RawMaterialTestData.SeedGeneralDataAsync(db, "Ingredient Source", "Plant Based"));

    private static async Task<Guid> SeedSupplierAsync(
        TestableVHSmartDbContext db,
        ManufacturerSupplierType type,
        string? manufacturerEmail = null,
        string? supplierEmail = null)
    {
        var row = new ManufacturerSupplierEntity
        {
            CompanyId = RawMaterialTestData.CompanyA,
            Type = type,
            ManufacturerName = type == ManufacturerSupplierType.SupplierOnly ? null : "Existing Maker",
            ManufacturerAddress = type == ManufacturerSupplierType.SupplierOnly ? null : "Jalan Lama 1",
            ManufacturerCountryId = type == ManufacturerSupplierType.SupplierOnly
                ? null
                : await RawMaterialTestData.MalaysiaIdAsync(db),
            ManufacturerEmail = manufacturerEmail,
            SupplierName = type == ManufacturerSupplierType.ManufacturerOnly ? null : "Existing Supplier",
            SupplierAddress = type == ManufacturerSupplierType.ManufacturerOnly ? null : "Jalan Lama 2",
            SupplierCountryId = type == ManufacturerSupplierType.ManufacturerOnly
                ? null
                : await RawMaterialTestData.MalaysiaIdAsync(db),
            SupplierEmail = supplierEmail
        };
        db.ManufacturerSuppliers.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static BulkUploadRawMaterialsCommand Command(
        byte[] bytes,
        string fileName = "raw-materials.xlsx") =>
        new(BulkUploadWorkbook.FormFile(bytes, fileName));

    private static Task<BulkUploadRawMaterialsResponse> Upload(
        VHSmartDbContext db,
        TestCurrentUser user,
        byte[] bytes,
        string fileName = "raw-materials.xlsx") =>
        new BulkUploadRawMaterialsHandler(db, user)
            .Handle(Command(bytes, fileName), CancellationToken.None);

    [Fact]
    public async Task Validator_NoFile_Fails()
    {
        var result = await new BulkUploadRawMaterialsValidator()
            .ValidateAsync(new BulkUploadRawMaterialsCommand(null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "File is required.");
    }

    [Fact]
    public async Task Handle_ValidRow_CreatesMaterialAndSupplierRowWithAccessibleRow()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);
        var (statusId, sourceId) = await SeedDropdownsAsync(db);

        var response = await Upload(db, user,
            BulkUploadRawMaterialTestData.File(BulkUploadRawMaterialTestData.Row()));

        Assert.Equal(1, response.TotalRows);
        Assert.Equal(1, response.UploadedRows);
        Assert.Empty(response.Errors);

        var material = await db.RawMaterials.AsNoTracking().SingleAsync();
        Assert.Equal(user.CompanyId, material.CompanyId);
        Assert.Equal(user.UserId, material.SysUserCreated);
        Assert.Equal(RawMaterialCategory.Core, material.Category);
        Assert.Equal("Badam Biji", material.Ingredient);
        Assert.Equal("BA-001", material.IngredientCode);
        Assert.Equal("SweetSpike", material.CommercialName);
        Assert.Equal(statusId, material.IngredientStatusId);
        Assert.Equal(sourceId, material.IngredientSourceId);
        Assert.False(material.IsPackagingMaterial);

        // No e-mail in either block: a new Manufacturer & Supplier row from both blocks (Q16).
        var supplier = await db.ManufacturerSuppliers.AsNoTracking().SingleAsync();
        Assert.Equal(material.ManufacturerSupplierId, supplier.Id);
        Assert.Equal(ManufacturerSupplierType.Both, supplier.Type);
        Assert.Equal("VH Distributor", supplier.SupplierName);
        Assert.Equal("MMK Trading", supplier.ManufacturerName);
        Assert.Equal("Level 7, Menara Binjai", supplier.SupplierAddress);
        Assert.Equal("No 2 Perindustrian Mara", supplier.ManufacturerAddress);
        Assert.Equal(await RawMaterialTestData.MalaysiaIdAsync(db), supplier.SupplierCountryId);
        Assert.Equal(await RawMaterialTestData.MalaysiaIdAsync(db), supplier.ManufacturerCountryId);
        Assert.Equal("Ahmad", supplier.SupplierPersonInCharge);
        Assert.Equal("0123456789", supplier.ManufacturerContactNo);
        Assert.Equal("MA12345XXXXXX", supplier.ManufacturerBusinessRegNo);
        Assert.Null(supplier.SupplierEmail); // empty cells are stored as NULL
        Assert.Null(supplier.ManufacturerEmail);
        Assert.Null(supplier.ManufacturerTypeId); // no CATEGORY column in this template (Q6)
        Assert.Null(supplier.ManufacturerWebpage);

        // The file has no "Accessible For" column: Database.md 8 keeps >= 1 row, shared with
        // the caller's own company.
        var accessible = await db.RawMaterialAccessibleCompanies.AsNoTracking().SingleAsync();
        Assert.Equal(material.Id, accessible.RawMaterialId);
        Assert.Equal(user.CompanyId, accessible.AccessibleCompanyId);
    }

    [Fact]
    public async Task Handle_SupplierBlockOnly_CreatesSupplierOnlyRow()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);
        await SeedDropdownsAsync(db);

        var response = await Upload(db, user,
            BulkUploadRawMaterialTestData.File(
                BulkUploadRawMaterialTestData.Row(manufacturerName: "")));

        Assert.Equal(1, response.UploadedRows);
        Assert.Empty(response.Errors);

        var supplier = await db.ManufacturerSuppliers.AsNoTracking().SingleAsync();
        Assert.Equal(ManufacturerSupplierType.SupplierOnly, supplier.Type);
        Assert.Equal("VH Distributor", supplier.SupplierName);
        Assert.Null(supplier.ManufacturerName);
        Assert.Null(supplier.ManufacturerAddress);
        Assert.Null(supplier.ManufacturerCountryId);
    }

    [Fact]
    public async Task Handle_ExistingSupplierEmail_ReusesRowWithoutValidatingItsBlock()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);
        await SeedDropdownsAsync(db);
        var seededId = await SeedSupplierAsync(
            db, ManufacturerSupplierType.SupplierOnly, supplierEmail: "vendor@x.example");

        // The supplier e-mail is the first candidate (its block comes first in the file) and
        // the hit is linked as-is - the manufacturer block of the file is not stored, and a
        // reused row is never re-validated (Q16), so its "Atlantis" country cannot fail.
        var response = await Upload(db, user,
            BulkUploadRawMaterialTestData.File(
                BulkUploadRawMaterialTestData.Row(
                    supplierEmail: "vendor@x.example",
                    manufacturerEmail: "maker@other.example",
                    manufacturerCountry: "Atlantis")));

        Assert.Equal(1, response.UploadedRows);
        Assert.Empty(response.Errors);

        var material = await db.RawMaterials.AsNoTracking().SingleAsync();
        Assert.Equal(seededId, material.ManufacturerSupplierId);
        Assert.Equal(1, await db.ManufacturerSuppliers.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Handle_ExistingManufacturerEmail_ReusesRow()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);
        await SeedDropdownsAsync(db);
        var seededId = await SeedSupplierAsync(
            db, ManufacturerSupplierType.Both, manufacturerEmail: "maker@x.example");

        var response = await Upload(db, user,
            BulkUploadRawMaterialTestData.File(
                BulkUploadRawMaterialTestData.Row(
                    supplierEmail: "",
                    manufacturerEmail: "maker@x.example")));

        Assert.Equal(1, response.UploadedRows);
        Assert.Empty(response.Errors);

        var material = await db.RawMaterials.AsNoTracking().SingleAsync();
        Assert.Equal(seededId, material.ManufacturerSupplierId);
        Assert.Equal(1, await db.ManufacturerSuppliers.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Handle_PartialFailure_ReportsPhysicalRowNumbers()
    {
        // Sheet rows: 1-3 legend, 4 header, 5 valid, 6 no block at all, 7 duplicate code.
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);
        await SeedDropdownsAsync(db);

        var response = await Upload(db, user,
            BulkUploadRawMaterialTestData.File(
                BulkUploadRawMaterialTestData.Row(),
                BulkUploadRawMaterialTestData.Row(
                    supplierName: "", manufacturerName: "", code: "BA-002"),
                BulkUploadRawMaterialTestData.Row(
                    ingredient: "Kacang Hijau", supplierName: "Other Supplier")));

        Assert.Equal(3, response.TotalRows);
        Assert.Equal(1, response.UploadedRows);
        Assert.Equal(2, response.Errors.Count);
        Assert.Equal(6, response.Errors[0].RowNumber);
        Assert.Equal("Manufacturer is required.", response.Errors[0].Message);
        Assert.Equal(7, response.Errors[1].RowNumber);
        Assert.Equal(
            "An ingredient with this code already exists.", response.Errors[1].Message);
        Assert.Single(await db.RawMaterials.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Handle_DuplicateCodeAgainstExisting_FailsRow()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);
        await SeedDropdownsAsync(db);
        await RawMaterialTestData.SeedRowAsync(db, ingredientCode: "BA-001");

        var response = await Upload(db, user,
            BulkUploadRawMaterialTestData.File(BulkUploadRawMaterialTestData.Row()));

        var error = Assert.Single(response.Errors);
        Assert.Equal(5, error.RowNumber);
        Assert.Equal("An ingredient with this code already exists.", error.Message);
        Assert.Equal(0, response.UploadedRows);
    }

    [Fact]
    public async Task Handle_UnknownStatusOrCategory_FailsEachRow()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);
        await SeedDropdownsAsync(db);

        var response = await Upload(db, user,
            BulkUploadRawMaterialTestData.File(
                BulkUploadRawMaterialTestData.Row(status: "Bogus"),
                BulkUploadRawMaterialTestData.Row(
                    ingredient: "Kacang Hijau", status: "", code: "BA-002"),
                BulkUploadRawMaterialTestData.Row(
                    ingredient: "Santan", category: "Vegetable", code: "BA-003")));

        Assert.Equal(0, response.UploadedRows);
        Assert.Equal(3, response.Errors.Count);
        Assert.Equal(5, response.Errors[0].RowNumber);
        Assert.Equal("Ingredient status not found.", response.Errors[0].Message);
        Assert.Equal(6, response.Errors[1].RowNumber);
        Assert.Equal("Ingredient status is required.", response.Errors[1].Message);
        Assert.Equal(7, response.Errors[2].RowNumber);
        Assert.Equal("Category is invalid.", response.Errors[2].Message);
    }

    [Fact]
    public async Task Handle_UnknownCountryInNewBlock_FailsRow()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);
        await SeedDropdownsAsync(db);

        // No e-mail anywhere, so the blocks have to build a new row - and a new row must
        // resolve its country (the Create rules).
        var response = await Upload(db, user,
            BulkUploadRawMaterialTestData.File(
                BulkUploadRawMaterialTestData.Row(supplierCountry: "Atlantis")));

        var error = Assert.Single(response.Errors);
        Assert.Equal(5, error.RowNumber);
        Assert.Equal("Country not found.", error.Message);
        Assert.Empty(await db.ManufacturerSuppliers.ToListAsync());
    }

    [Fact]
    public async Task Handle_NaSupplierAndEmptyManufacturer_FailsRow()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);
        await SeedDropdownsAsync(db);

        // "N/A" is the template's "no data" placeholder (Q6): with both blocks blank the row
        // fails with the Create text.
        var response = await Upload(db, user,
            BulkUploadRawMaterialTestData.File(
                BulkUploadRawMaterialTestData.Row(supplierName: "N/A", manufacturerName: "")));

        var error = Assert.Single(response.Errors);
        Assert.Equal(5, error.RowNumber);
        Assert.Equal("Manufacturer is required.", error.Message);
        Assert.Empty(await db.RawMaterials.ToListAsync());
    }

    [Fact]
    public async Task Handle_BlankRows_AreIgnored()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);
        await SeedDropdownsAsync(db);

        var response = await Upload(db, user,
            BulkUploadRawMaterialTestData.File(
                BulkUploadRawMaterialTestData.Row(),
                [" "],
                [" "],
                BulkUploadRawMaterialTestData.Row(
                    ingredient: "Kacang Hijau", code: "BA-002")));

        Assert.Equal(2, response.TotalRows);
        Assert.Equal(2, response.UploadedRows);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public async Task Handle_NoDataRows_Throws()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, BulkUploadRawMaterialTestData.File()));

        Assert.Equal("The file contains no data rows.", exception.Message);
    }

    [Fact]
    public async Task Handle_MoreThan250Rows_Throws()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);
        await SeedDropdownsAsync(db);
        var rows = Enumerable.Range(0, 251)
            .Select(i => BulkUploadRawMaterialTestData.Row(
                ingredient: $"Ingredient {i}", code: $"BA-{i:000}"))
            .ToArray();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, BulkUploadRawMaterialTestData.File(rows)));

        Assert.Equal("The file exceeds the limit of 250 rows.", exception.Message);
        Assert.Empty(await db.RawMaterials.ToListAsync());
    }

    [Fact]
    public async Task Handle_WrongExtension_Throws()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user,
                BulkUploadRawMaterialTestData.File(BulkUploadRawMaterialTestData.Row()),
                "raw-materials.csv"));

        Assert.Equal("File type is not allowed. Allowed types: xls, xlsx.", exception.Message);
    }

    [Fact]
    public async Task Handle_CorruptBytes_Throws()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, [1, 2, 3, 4]));

        Assert.Equal("The file could not be read as an Excel workbook.", exception.Message);
    }

    [Fact]
    public async Task Handle_MissingRequiredColumn_Throws()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);
        var headerWithoutSource = BulkUploadRawMaterialTestData.Header
            .Where(cell => !cell.Contains(
                "INGREDIENT/RAW MATERIAL SOURCE", StringComparison.Ordinal))
            .ToArray();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, BulkUploadWorkbook.Sheet(
                [.. BulkUploadRawMaterialTestData.Preamble, headerWithoutSource,
                    BulkUploadRawMaterialTestData.Row()])));

        Assert.Equal(
            "The file does not match the raw material bulk upload template.",
            exception.Message);
    }

    [Fact]
    public async Task Handle_NoHeaderRow_Throws()
    {
        var user = RawMaterialTestData.UserA();
        var db = await RawMaterialTestData.CreateDbAsync(user);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, BulkUploadWorkbook.Sheet(
                [.. BulkUploadRawMaterialTestData.Preamble,
                    ["NAME", "STATUS"],
                    ["Badam Biji", "Active"]])));

        Assert.Equal(
            "The file does not match the raw material bulk upload template.",
            exception.Message);
    }
}
