using FluentValidation;
using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Premise.ManagePremise;
using VHSmart_Api.Shared.Domain.Companies;
using VHSmart_Api.Shared.Exceptions;
using VHSmart_Api.Shared.Infrastructure.Persistence;

namespace VHSmart_Api.Tests.Features.Premise.ManagePremise;

// Bulk upload (spec 7.7 + 21.10): .xls/.xlsx, header row 4, max 250 data rows, partial
// success with one first-error-per-row list using physical sheet row numbers.
public class BulkUploadPremisesTests
{
    private static BulkUploadPremisesCommand Command(byte[] bytes, string fileName = "premises.xlsx") =>
        new(BulkUploadTestData.FormFile(bytes, fileName));

    private static Task<BulkUploadPremisesResponse> Upload(
        VHSmartDbContext db,
        TestCurrentUser user,
        byte[] bytes,
        string fileName = "premises.xlsx") =>
        new BulkUploadPremisesHandler(db, user)
            .Handle(Command(bytes, fileName), CancellationToken.None);

    [Fact]
    public async Task Validator_NoFile_Fails()
    {
        var result = await new BulkUploadPremisesValidator()
            .ValidateAsync(new BulkUploadPremisesCommand(null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,
            failure => failure.ErrorMessage == "File is required.");
    }

    [Fact]
    public async Task Handle_ValidFactoryFile_UploadsRowsWithAuditStamp()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);

        var response = await Upload(db, user, BulkUploadTestData.FactoryFile(
            BulkUploadTestData.FactoryRow("one@premise.my"),
            BulkUploadTestData.FactoryRow("two@premise.my", name: "Second Factory")));

        Assert.Equal(2, response.TotalRows);
        Assert.Equal(2, response.UploadedRows);
        Assert.Empty(response.Errors);
        var stored = await db.Premises.AsNoTracking().ToListAsync();
        Assert.Equal(2, stored.Count);
        var first = stored.Single(row => row.Email == "one@premise.my");
        Assert.Equal(user.CompanyId, first.CompanyId);
        Assert.Equal(user.UserId, first.SysUserCreated);
        Assert.Equal(PremiseType.Factory, first.PremiseType);
        Assert.Equal("Seksyen 7", first.Address3);
        Assert.Equal("Malaysia", (await db.Countries.SingleAsync(c => c.Id == first.CountryId)).Name);
        // No STATE column in the owner's file; the template's dropdown value is stored as typed.
        Assert.Equal("", first.State);
        Assert.Equal("Active", first.Status);
        Assert.Null(first.StoreCode);
    }

    [Fact]
    public async Task Handle_RestaurantFile_TypeAliasAndStoreCode_Parsed()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);

        var response = await Upload(db, user, BulkUploadTestData.RestaurantFile(
            BulkUploadTestData.RestaurantRow("cafe@premise.my", storeCode: "SC-CAFE")));

        Assert.Equal(1, response.UploadedRows);
        Assert.Empty(response.Errors);
        var stored = await db.Premises.AsNoTracking().SingleAsync();
        Assert.Equal(PremiseType.RestaurantsAndCafe, stored.PremiseType);
        Assert.Equal("SC-CAFE", stored.StoreCode);
    }

    [Fact]
    public async Task Handle_PartialFailure_ReportsPhysicalRowNumbers()
    {
        // Sheet rows: 1-3 legend, 4 header, 5 valid, 6 duplicate e-mail, 7 no postcode.
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);
        await PremiseTestData.SeedPremiseAsync(db, user.CompanyId, email: "taken@premise.my");

        var response = await Upload(db, user, BulkUploadTestData.FactoryFile(
            BulkUploadTestData.FactoryRow("fresh@premise.my"),
            BulkUploadTestData.FactoryRow("taken@premise.my"),
            BulkUploadTestData.FactoryRow("nopost@premise.my", postcode: "   ")));

        Assert.Equal(3, response.TotalRows);
        Assert.Equal(1, response.UploadedRows);
        Assert.Equal(2, response.Errors.Count);
        Assert.Equal(6, response.Errors[0].RowNumber);
        Assert.Equal("A premise with this e-mail already exists.", response.Errors[0].Message);
        Assert.Equal(7, response.Errors[1].RowNumber);
        Assert.Equal("Postcode is required.", response.Errors[1].Message);
        Assert.NotNull(await db.Premises.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Email == "fresh@premise.my"));
    }

    [Fact]
    public async Task Handle_CompanyCellNotOwnCompany_FailsRow()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);

        var response = await Upload(db, user, BulkUploadTestData.FactoryFile(
            BulkUploadTestData.FactoryRow("x@premise.my", company: "Somebody Else Sdn Bhd")));

        Assert.Equal(1, response.TotalRows);
        Assert.Equal(0, response.UploadedRows);
        var error = Assert.Single(response.Errors);
        Assert.Equal(5, error.RowNumber);
        Assert.Equal("Company not found.", error.Message);
        Assert.Empty(await db.Premises.ToListAsync());
    }

    [Fact]
    public async Task Handle_DuplicateEmailWithinBatch_FailsSecondRow()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);

        var response = await Upload(db, user, BulkUploadTestData.FactoryFile(
            BulkUploadTestData.FactoryRow("same@premise.my"),
            BulkUploadTestData.FactoryRow("same@premise.my")));

        Assert.Equal(1, response.UploadedRows);
        var error = Assert.Single(response.Errors);
        Assert.Equal(6, error.RowNumber);
        Assert.Equal("A premise with this e-mail already exists.", error.Message);
    }

    [Fact]
    public async Task Handle_DuplicateStoreCodeAgainstExisting_FailsRow()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);
        await PremiseTestData.SeedPremiseAsync(db, user.CompanyId, storeCode: "SC-01");

        var response = await Upload(db, user, BulkUploadTestData.RestaurantFile(
            BulkUploadTestData.RestaurantRow("new@premise.my", storeCode: "SC-01")));

        var error = Assert.Single(response.Errors);
        Assert.Equal(5, error.RowNumber);
        Assert.Equal("A premise with this store code already exists.", error.Message);
    }

    [Fact]
    public async Task Handle_UnknownCountry_FailsRow()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);

        var response = await Upload(db, user, BulkUploadTestData.FactoryFile(
            BulkUploadTestData.FactoryRow("atlantis@premise.my", country: "Atlantis")));

        var error = Assert.Single(response.Errors);
        Assert.Equal(5, error.RowNumber);
        Assert.Equal("Country not found.", error.Message);
    }

    [Fact]
    public async Task Handle_BlankOrInvalidType_FailsEachRow()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);

        var response = await Upload(db, user, BulkUploadTestData.FactoryFile(
            BulkUploadTestData.FactoryRow("blank@premise.my", type: ""),
            BulkUploadTestData.FactoryRow("bad@premise.my", type: "Warehouse")));

        Assert.Equal(0, response.UploadedRows);
        Assert.Equal(2, response.Errors.Count);
        Assert.Equal(5, response.Errors[0].RowNumber);
        Assert.Equal("Premise type is required.", response.Errors[0].Message);
        Assert.Equal(6, response.Errors[1].RowNumber);
        Assert.Equal("Premise type is invalid.", response.Errors[1].Message);
    }

    [Fact]
    public async Task Handle_BlankRows_AreIgnored()
    {
        // The owner's templates carry a formatted but empty tail (rows 5-50).
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);

        var response = await Upload(db, user, BulkUploadTestData.FactoryFile(
            BulkUploadTestData.FactoryRow("first@premise.my"),
            [" "],
            [" "],
            BulkUploadTestData.FactoryRow("second@premise.my")));

        Assert.Equal(2, response.TotalRows);
        Assert.Equal(2, response.UploadedRows);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public async Task Handle_NoDataRows_Throws()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, BulkUploadTestData.FactoryFile()));

        Assert.Equal("The file contains no data rows.", exception.Message);
    }

    [Fact]
    public async Task Handle_MoreThan250Rows_Throws()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);
        var rows = Enumerable.Range(0, 251)
            .Select(i => BulkUploadTestData.FactoryRow(
                $"bulk{i}@premise.my", name: $"Premise {i}"))
            .ToArray();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, BulkUploadTestData.FactoryFile(rows)));

        Assert.Equal("The file exceeds the limit of 250 rows.", exception.Message);
        Assert.Empty(await db.Premises.ToListAsync());
    }

    [Fact]
    public async Task Handle_WrongExtension_Throws()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user,
                BulkUploadTestData.FactoryFile(
                    BulkUploadTestData.FactoryRow("csv@premise.my")),
                "premises.csv"));

        Assert.Equal("File type is not allowed. Allowed types: xls, xlsx.", exception.Message);
    }

    [Fact]
    public async Task Handle_CorruptBytes_Throws()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, [1, 2, 3, 4]));

        Assert.Equal("The file could not be read as an Excel workbook.", exception.Message);
    }

    [Fact]
    public async Task Handle_MissingRequiredColumn_Throws()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);
        var headerWithoutCountry = BulkUploadTestData.FactoryHeader
            .Where(cell => !cell.StartsWith("COUNTRY", StringComparison.Ordinal))
            .ToArray();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, BulkUploadTestData.Sheet(
                [.. BulkUploadTestData.Preamble, headerWithoutCountry,
                    BulkUploadTestData.FactoryRow("noCountry@premise.my")])));

        Assert.Equal(
            "The file does not match the premise bulk upload template.", exception.Message);
    }

    [Fact]
    public async Task Handle_NoPremiseTypeHeader_Throws()
    {
        var user = PremiseTestData.CompanyUser();
        var db = await PremiseTestData.CreateDbAsync(user);
        await PremiseTestData.SeedCompanyAsync(db, companyId: user.CompanyId);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Upload(db, user, BulkUploadTestData.Sheet(
                [.. BulkUploadTestData.Preamble,
                    ["FOR  COMPANY (*)", "PREMISE NAME (*)"],
                    ["Verify Halal Sdn Bhd", "Somewhere"]])));

        Assert.Equal(
            "The file does not match the premise bulk upload template.", exception.Message);
    }
}
