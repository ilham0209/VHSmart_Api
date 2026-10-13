using ClosedXML.Excel;
using VHSmart_Api.Features.AdvancedSearch;
using VHSmart_Api.Shared.Domain.Calculators;
using VHSmart_Api.Shared.Domain.HalalApplication;
using VHSmart_Api.Shared.Models;

namespace VHSmart_Api.Tests.Features.AdvancedSearch;

// Advanced Search > Premise view (spec 8): the 19 filters over one seeded world (premise
// owned + halal chain + document status shapes), the derived-column fill, paging through the
// DataGrid plumbing, the Excel export (opened back with ClosedXML) and the screen-local
// option lists. The foreign tenant's premise is seeded to match most own-company filters so
// the CompanyId guard is asserted on every path.
public class PremiseSearchTests
{
    private static Task<DataGridResponse<SearchPremisesResponse>> SearchAsync(
        TestableVHSmartDbContext db,
        TestCurrentUser user,
        Action<SearchPremisesQuery>? configure = null)
    {
        var query = new SearchPremisesQuery();
        configure?.Invoke(query);
        return new SearchPremisesHandler(db, user).Handle(query, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_NoFilters_ReturnsOwnPremisesSortedWithDerivedColumns()
    {
        var user = PremiseSearchTestData.User();
        var db = await PremiseSearchTestData.CreateDbAsync(user);
        var world = await PremiseSearchTestData.SeedSearchWorldAsync(db);

        var grid = await SearchAsync(db, user);

        Assert.Equal(4, grid.TotalRecords);
        var rows = grid.Data.ToList();
        Assert.Equal(
            ["Delta Restoran", "Gamma Cafe", "Kedai Kopi Dummy", "Seri Rasa Factory"],
            rows.Select(row => row.StoreName));

        var alpha = Assert.Single(rows, row => row.Id == world.Alpha);
        Assert.Equal("SR-001", alpha.StoreCode);
        Assert.Equal("1 Jalan Verify Taman Industri", alpha.Address);
        Assert.Equal("Seri Rasa", alpha.Brand);
        Assert.Equal("Sereni Dining", alpha.MenuConcept);
        Assert.Equal("Amy Rashid", alpha.AreaManager);
        Assert.Equal("Bob Tan", alpha.OperationManager);
        Assert.Equal("Complete Documentation", alpha.PremiseTag);
        Assert.Equal("https://maps.example/alpha", alpha.PremiseLocationUrl);
        Assert.Equal("NOT COMPLETE DOCUMENTATION", alpha.PremiseDocumentStatus);
        var halal = Assert.Single(alpha.HalalInformation!);
        Assert.Equal("VHS/2026/0001", halal.VhSmartReferenceNo);
        Assert.Equal("CB-001", halal.CbReferenceNo);
        Assert.Equal(ApplicationStatus.ApplicationApproved, halal.ApplicationStatus);
        Assert.Equal("HAL-VALID-001", halal.HalalCertificateNo);
        Assert.Equal(HalalStatus.Valid, halal.HalalCertificateStatus);
        Assert.Equal(new DateOnly(2027, 6, 30), halal.HalalExpiryDate);
        Assert.False(string.IsNullOrEmpty(halal.Scheme));

        var gamma = Assert.Single(rows, row => row.Id == world.Gamma);
        Assert.Equal("COMPLETE DOCUMENTATION", gamma.PremiseDocumentStatus);
        Assert.Empty(gamma.HalalInformation!);
        var delta = Assert.Single(rows, row => row.Id == world.Delta);
        Assert.Equal("2 OF THE DOCUMENT HAS EXPIRED", delta.PremiseDocumentStatus);
        Assert.DoesNotContain(rows, row => row.Id == world.ForeignPremise);
    }

    [Fact]
    public async Task Handle_TextFilters_MatchContainsIgnoringCase()
    {
        var user = PremiseSearchTestData.User();
        var db = await PremiseSearchTestData.CreateDbAsync(user);
        var world = await PremiseSearchTestData.SeedSearchWorldAsync(db);

        var byStoreCode = await SearchAsync(db, user, query => query.StoreCode = "sr-");
        Assert.Equal(world.Alpha, Assert.Single(byStoreCode.Data).Id);

        var byStoreName = await SearchAsync(db, user, query => query.StoreName = "KOPI");
        Assert.Equal(world.Beta, Assert.Single(byStoreName.Data).Id);
    }

    [Fact]
    public async Task Handle_DropdownFilters_MatchExactly()
    {
        var user = PremiseSearchTestData.User();
        var db = await PremiseSearchTestData.CreateDbAsync(user);
        var world = await PremiseSearchTestData.SeedSearchWorldAsync(db);

        var byBrand = await SearchAsync(db, user, query => query.BrandId = world.BrandSeriRasa);
        Assert.Equal(world.Alpha, Assert.Single(byBrand.Data).Id);

        var byConcept = await SearchAsync(
            db, user, query => query.MenuConceptId = world.ConceptKopiBar);
        Assert.Equal(world.Beta, Assert.Single(byConcept.Data).Id);

        var byStatus = await SearchAsync(db, user, query => query.PremiseStatus = "inactive");
        Assert.Equal(world.Beta, Assert.Single(byStatus.Data).Id);

        var byTag = await SearchAsync(
            db, user, query => query.PremiseTagId = world.TagCompleteDocumentation);
        Assert.Equal(world.Alpha, Assert.Single(byTag.Data).Id);

        var byState = await SearchAsync(db, user, query => query.State = "JOHOR");
        Assert.Equal(world.Beta, Assert.Single(byState.Data).Id);

        var byAreaManager = await SearchAsync(
            db, user, query => query.AreaManagerStaffId = world.AreaManager);
        Assert.Equal(world.Alpha, Assert.Single(byAreaManager.Data).Id);

        var byOperationManager = await SearchAsync(
            db, user, query => query.OperationManagerStaffId = world.OperationManager);
        Assert.Equal(world.Alpha, Assert.Single(byOperationManager.Data).Id);
    }

    [Fact]
    public async Task Handle_DateRangeFilters_AreInclusiveOnBothEnds()
    {
        var user = PremiseSearchTestData.User();
        var db = await PremiseSearchTestData.CreateDbAsync(user);
        var world = await PremiseSearchTestData.SeedSearchWorldAsync(db);

        // One day where Alpha opens - both ends inclusive; premises without the date drop out.
        var openingDay = await SearchAsync(db, user, query =>
        {
            query.OpeningDateFrom = new DateOnly(2026, 6, 15);
            query.OpeningDateTo = new DateOnly(2026, 6, 15);
        });
        Assert.Equal(world.Alpha, Assert.Single(openingDay.Data).Id);

        var openingRange = await SearchAsync(db, user, query =>
        {
            query.OpeningDateFrom = new DateOnly(2025, 1, 1);
            query.OpeningDateTo = new DateOnly(2025, 12, 31);
        });
        Assert.Equal(world.Beta, Assert.Single(openingRange.Data).Id);

        // Only Alpha carries a closing date; the upper bound covers its whole day.
        var closingRange = await SearchAsync(db, user, query =>
        {
            query.ClosingDateFrom = new DateOnly(2026, 1, 1);
            query.ClosingDateTo = new DateOnly(2026, 11, 30);
        });
        Assert.Equal(world.Alpha, Assert.Single(closingRange.Data).Id);
    }

    [Fact]
    public async Task Handle_HalalFilters_MatchThroughBatchChain()
    {
        var user = PremiseSearchTestData.User();
        var db = await PremiseSearchTestData.CreateDbAsync(user);
        var world = await PremiseSearchTestData.SeedSearchWorldAsync(db);

        var byReferenceNo = await SearchAsync(
            db, user, query => query.VhSmartReferenceNo = "0001");
        Assert.Equal(world.Alpha, Assert.Single(byReferenceNo.Data).Id);

        var byCbReferenceNo = await SearchAsync(
            db, user, query => query.CbReferenceNo = "cb-001");
        Assert.Equal(world.Alpha, Assert.Single(byCbReferenceNo.Data).Id);

        var byApplicationStatus = await SearchAsync(
            db, user, query => query.HalalApplicationStatus = "draft");
        Assert.Equal(world.Beta, Assert.Single(byApplicationStatus.Data).Id);

        var byCertificateNo = await SearchAsync(
            db, user, query => query.HalalCertificateNo = "expired-001");
        Assert.Equal(world.Beta, Assert.Single(byCertificateNo.Data).Id);

        // D-04 mirror: Alpha's certificate (2027-06-30) is Valid, Beta's (2024-01-15) Expired.
        var expired = await SearchAsync(
            db, user, query => query.HalalCertificateStatus = "Expired");
        Assert.Equal(world.Beta, Assert.Single(expired.Data).Id);
        var valid = await SearchAsync(db, user, query => query.HalalCertificateStatus = "valid");
        Assert.Equal(world.Alpha, Assert.Single(valid.Data).Id);

        var expiryRange = await SearchAsync(db, user, query =>
        {
            query.HalalExpiryFrom = new DateOnly(2024, 1, 1);
            query.HalalExpiryTo = new DateOnly(2024, 12, 31);
        });
        Assert.Equal(world.Beta, Assert.Single(expiryRange.Data).Id);
    }

    [Fact]
    public async Task Handle_DocumentStatusFilter_MatchesCalculatorTexts()
    {
        var user = PremiseSearchTestData.User();
        var db = await PremiseSearchTestData.CreateDbAsync(user);
        var world = await PremiseSearchTestData.SeedSearchWorldAsync(db);

        var complete = await SearchAsync(
            db, user, query => query.PremiseDocumentStatus = PremiseDocumentStatusCalculator.CompleteText);
        Assert.Equal(world.Gamma, Assert.Single(complete.Data).Id);

        var notComplete = await SearchAsync(
            db, user, query => query.PremiseDocumentStatus = PremiseDocumentStatusCalculator.NotCompleteText);

        // Alpha and Beta carry no attachments; Gamma is complete and Delta "2 of ... expired".
        Assert.Equal(2, notComplete.TotalRecords);
        Assert.DoesNotContain(notComplete.Data, row => row.Id == world.Gamma);
        Assert.DoesNotContain(notComplete.Data, row => row.Id == world.Delta);

        var expiredTwo = await SearchAsync(
            db, user, query => query.PremiseDocumentStatus = PremiseDocumentStatusCalculator.ExpiredText(2));
        Assert.Equal(world.Delta, Assert.Single(expiredTwo.Data).Id);
    }

    [Fact]
    public async Task Handle_FilterMatchingForeignRowOnly_ReturnsEmpty()
    {
        var user = PremiseSearchTestData.User();
        var db = await PremiseSearchTestData.CreateDbAsync(user);
        await PremiseSearchTestData.SeedSearchWorldAsync(db);

        var grid = await SearchAsync(db, user, query => query.StoreCode = "FX-999");

        Assert.Equal(0, grid.TotalRecords);
        Assert.Empty(grid.Data);
    }

    [Fact]
    public async Task Handle_PageTwo_UsesDataGridPlumbing()
    {
        var user = PremiseSearchTestData.User();
        var db = await PremiseSearchTestData.CreateDbAsync(user);
        await PremiseSearchTestData.SeedSearchWorldAsync(db);

        var first = await SearchAsync(db, user, query =>
        {
            query.Request.Page = 1;
            query.Request.PageSize = 2;
        });
        Assert.Equal(4, first.TotalRecords);
        Assert.Equal(2, first.TotalPages);
        Assert.True(first.HasNextPage);
        Assert.Equal(
            ["Delta Restoran", "Gamma Cafe"],
            first.Data.Select(row => row.StoreName));

        var second = await SearchAsync(db, user, query =>
        {
            query.Request.Page = 2;
            query.Request.PageSize = 2;
        });
        Assert.False(second.HasNextPage);
        Assert.Equal(
            ["Kedai Kopi Dummy", "Seri Rasa Factory"],
            second.Data.Select(row => row.StoreName));
    }

    [Fact]
    public void Validator_EmptyQueryAndEqualRanges_AreValid()
    {
        var validator = new SearchPremisesValidator();

        Assert.True(validator.Validate(new SearchPremisesQuery()).IsValid);
        Assert.True(validator.Validate(new SearchPremisesQuery
        {
            HalalExpiryFrom = new DateOnly(2026, 1, 1),
            HalalExpiryTo = new DateOnly(2026, 1, 1),
            OpeningDateFrom = new DateOnly(2026, 2, 1),
            OpeningDateTo = new DateOnly(2026, 3, 1),
            ClosingDateFrom = new DateOnly(2026, 4, 1),
            ClosingDateTo = new DateOnly(2026, 5, 1)
        }).IsValid);
        Assert.True(validator.Validate(new SearchPremisesQuery
        {
            HalalCertificateStatus = "valid"
        }).IsValid);
    }

    [Fact]
    public void Validator_ReversedRangesAndUnknownCertificateStatus_AreInvalid()
    {
        var validator = new SearchPremisesValidator();

        var reversedOpening = validator.Validate(new SearchPremisesQuery
        {
            OpeningDateFrom = new DateOnly(2026, 5, 1),
            OpeningDateTo = new DateOnly(2026, 1, 1)
        });
        Assert.False(reversedOpening.IsValid);
        Assert.Contains(
            reversedOpening.Errors,
            error => error.ErrorMessage
                == "Opening Date from must not be after the to date.");

        var reversedClosing = validator.Validate(new SearchPremisesQuery
        {
            ClosingDateFrom = new DateOnly(2026, 5, 1),
            ClosingDateTo = new DateOnly(2026, 1, 1)
        });
        Assert.False(reversedClosing.IsValid);

        var reversedHalal = validator.Validate(new SearchPremisesQuery
        {
            HalalExpiryFrom = new DateOnly(2026, 5, 1),
            HalalExpiryTo = new DateOnly(2026, 1, 1)
        });
        Assert.False(reversedHalal.IsValid);

        var unknownStatus = validator.Validate(new SearchPremisesQuery
        {
            HalalCertificateStatus = "Maybe"
        });
        Assert.False(unknownStatus.IsValid);
        Assert.Contains(
            unknownStatus.Errors,
            error => error.ErrorMessage
                == "Halal Certificate Status must be Valid or Expired.");
    }

    [Fact]
    public void ExportValidator_ReversedClosingRange_IsInvalid()
    {
        var validator = new ExportPremisesValidator();

        var result = validator.Validate(new ExportPremisesToExcelQuery
        {
            ClosingDateFrom = new DateOnly(2026, 5, 1),
            ClosingDateTo = new DateOnly(2026, 1, 1)
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Export_WithFilter_WritesWorkbookMatchingTheGrid()
    {
        var user = PremiseSearchTestData.User();
        var db = await PremiseSearchTestData.CreateDbAsync(user);
        await PremiseSearchTestData.SeedSearchWorldAsync(db);

        var response = await new ExportPremisesToExcelHandler(db, user)
            .Handle(
                new ExportPremisesToExcelQuery { StoreCode = "SR-" },
                CancellationToken.None);

        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.ContentType);
        Assert.StartsWith("AdvancedSearch-Premise-", response.FileName);
        Assert.EndsWith(".xlsx", response.FileName);

        using var stream = new MemoryStream(response.Content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Premise");
        Assert.Equal("Store Code", sheet.Cell(1, 1).GetString());
        Assert.Equal("Establishment Name", sheet.Cell(1, 2).GetString());
        Assert.Equal("View Premise Location", sheet.Cell(1, 6).GetString());
        Assert.Equal("Halal Information", sheet.Cell(1, 11).GetString());
        Assert.Equal("Premise Document Status", sheet.Cell(1, 12).GetString());
        Assert.Equal(2, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal("SR-001", sheet.Cell(2, 1).GetString());
        Assert.Equal("Seri Rasa Factory", sheet.Cell(2, 2).GetString());
        Assert.Equal("Seri Rasa", sheet.Cell(2, 7).GetString());
        var halalInformation = sheet.Cell(2, 11).GetString();
        Assert.Contains("1. VHS/2026/0001", halalInformation);
        Assert.Contains("CB-001", halalInformation);
        Assert.Contains("HAL-VALID-001", halalInformation);
        Assert.Contains("Valid", halalInformation);
        Assert.Contains("30/06/2027", halalInformation);
        Assert.Equal("NOT COMPLETE DOCUMENTATION", sheet.Cell(2, 12).GetString());
    }

    [Fact]
    public async Task Export_NoFilters_WritesOneRowPerPremise()
    {
        var user = PremiseSearchTestData.User();
        var db = await PremiseSearchTestData.CreateDbAsync(user);
        await PremiseSearchTestData.SeedSearchWorldAsync(db);

        var response = await new ExportPremisesToExcelHandler(db, user)
            .Handle(new ExportPremisesToExcelQuery(), CancellationToken.None);

        using var stream = new MemoryStream(response.Content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Premise");

        // Header + the four own-company premises sorted by name; the foreign row never
        // exports. Delta leads (row 2), Alpha trails (row 5).
        Assert.Equal(5, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal("Delta Restoran", sheet.Cell(2, 2).GetString());
        Assert.Equal(
            PremiseDocumentStatusCalculator.ExpiredText(2),
            sheet.Cell(2, 12).GetString());
        Assert.Equal("Kedai Kopi Dummy", sheet.Cell(4, 2).GetString());
        Assert.Equal(
            PremiseDocumentStatusCalculator.NotCompleteText,
            sheet.Cell(5, 12).GetString());
    }

    [Fact]
    public async Task Options_ReturnScreenLocalListsScopedToTheCompany()
    {
        var user = PremiseSearchTestData.User();
        var db = await PremiseSearchTestData.CreateDbAsync(user);
        await PremiseSearchTestData.SeedSearchWorldAsync(db);

        var options = await new GetPremiseSearchOptionsHandler(db, user)
            .Handle(new GetPremiseSearchOptionsQuery(), CancellationToken.None);

        Assert.Equal(["ACTIVE", "INACTIVE"], options.PremiseStatuses);
        Assert.Equal(
            [
                PremiseDocumentStatusCalculator.CompleteText,
                PremiseDocumentStatusCalculator.NotCompleteText,
                PremiseDocumentStatusCalculator.ExpiredText(1),
                PremiseDocumentStatusCalculator.ExpiredText(2),
                PremiseDocumentStatusCalculator.ExpiredText(3),
                PremiseDocumentStatusCalculator.ExpiredText(4),
                PremiseDocumentStatusCalculator.ExpiredText(5)
            ],
            options.PremiseDocumentStatuses);
        Assert.Equal(
            [ApplicationStatus.ApplicationApproved, ApplicationStatus.Draft],
            options.HalalApplicationStatuses);
        Assert.Equal(["Valid", "Expired"], options.HalalCertificateStatuses);
        Assert.Equal(
            ["Kopi Line", "Seri Rasa"],
            options.Brands.Select(option => option.Name));
        Assert.Equal(
            ["Complete Documentation", "Partial Documentation"],
            options.PremiseTags.Select(option => option.Name));
        Assert.Equal(
            ["Kopi Bar", "Sereni Dining"],
            options.MenuConcepts.Select(option => option.Name));
        Assert.Equal(
            ["Amy Rashid", "Bob Tan"],
            options.Staff.Select(option => option.Name));
    }
}
