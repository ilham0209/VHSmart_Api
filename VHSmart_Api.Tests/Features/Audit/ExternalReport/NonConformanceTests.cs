using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.ExternalReport;
using VHSmart_Api.Shared.Domain.Audit;
using VHSmart_Api.Shared.Exceptions;
using static VHSmart_Api.Tests.Features.Audit.ExternalReport.ExternalReportTestData;

namespace VHSmart_Api.Tests.Features.Audit.ExternalReport;

public class NonConformanceTests
{
    private static CreateNonConformanceCommand CreateCommand(
        Guid reportId,
        Guid categoryId,
        string remarks = "Logged on upload") => new(
        reportId,
        "Seri Rasa Factory",
        "Rendang product",
        "REF-1",
        "Clause 2.1",
        "Pest traces found",
        categoryId,
        remarks);

    [Fact]
    public async Task Create_Valid_CreatesExternalOpenNc()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);
        var categoryId = await SeedNonComplianceCategoryAsync(db);

        var response = await new CreateNonConformanceHandler(db, user).Handle(
            CreateCommand(reportId, categoryId), CancellationToken.None);

        var nc = await db.NonConformances.SingleAsync(row => row.Id == response.Id);
        Assert.Equal(NonConformanceSource.External, nc.Source);
        Assert.Equal(NonConformanceStatus.Open, nc.Status);
        Assert.False(nc.IsFinal);
        Assert.Equal(reportId, nc.ExternalAuditReportId);
        Assert.Equal("Critical", response.Category);
        Assert.Null(response.CorrectiveActionDetails);
    }

    [Fact]
    public async Task Create_UnknownReport_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedNonComplianceCategoryAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CreateNonConformanceHandler(db, user).Handle(
                CreateCommand(Guid.NewGuid(), categoryId), CancellationToken.None));
    }

    [Fact]
    public async Task Create_ForeignCategory_FailsValidation()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);
        var foreignCategory = await SeedGeneralDataAsync(
            db, CompanyB, "Non Compliance Category", "Critical");

        var results = await new CreateNonConformanceValidator(db, user)
            .ValidateAsync(CreateCommand(reportId, foreignCategory));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Category not found.");
    }

    [Fact]
    public async Task Create_WrongGeneralDataPair_FailsValidation()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);
        var wrongCategory = await SeedGeneralDataAsync(
            db, CompanyA, "Audit Purpose", "Routine Audit");

        var results = await new CreateNonConformanceValidator(db, user)
            .ValidateAsync(CreateCommand(reportId, wrongCategory));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Category not found.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Create_MissingRemarks_FailsValidation(string? remarks)
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);
        var categoryId = await SeedNonComplianceCategoryAsync(db);

        var results = await new CreateNonConformanceValidator(db, user)
            .ValidateAsync(CreateCommand(reportId, categoryId, remarks!));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Remarks is required.");
    }

    [Fact]
    public async Task Update_Valid_UpdatesFieldsAndKeepsLifecycleColumns()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);
        var categoryId = await SeedNonComplianceCategoryAsync(db);
        var ncId = await SeedNonConformanceAsync(db, CompanyA, reportId, categoryId);

        var response = await new UpdateNonConformanceHandler(db, user).Handle(
            new UpdateNonConformanceCommand(
                ncId, "Kedai Serai Corner", "Nasi Lemak", "REF-2", "Clause 3.2",
                "Temperature log missing", categoryId, "Updated remarks"),
            CancellationToken.None);

        Assert.Equal("Kedai Serai Corner", response.ItemName);
        Assert.Equal("Temperature log missing", response.NonConformanceDetails);
        var nc = await db.NonConformances.SingleAsync(row => row.Id == ncId);
        Assert.Equal(NonConformanceSource.External, nc.Source);
        Assert.Equal(NonConformanceStatus.Open, nc.Status);
        Assert.False(nc.IsFinal);
    }

    [Fact]
    public async Task Update_Unknown_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var categoryId = await SeedNonComplianceCategoryAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateNonConformanceHandler(db, user).Handle(
                new UpdateNonConformanceCommand(
                    Guid.NewGuid(), null, null, null, null, null, categoryId, "r"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Update_ForeignNc_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyB, cb, scheme, [premise]);
        var categoryId = await SeedGeneralDataAsync(
            db, CompanyB, "Non Compliance Category", "Critical");
        var ncId = await SeedNonConformanceAsync(db, CompanyB, reportId, categoryId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateNonConformanceHandler(db, user).Handle(
                new UpdateNonConformanceCommand(
                    ncId, null, null, null, null, null, categoryId, "r"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Delete_Valid_SoftDeletes()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);
        var categoryId = await SeedNonComplianceCategoryAsync(db);
        var ncId = await SeedNonConformanceAsync(db, CompanyA, reportId, categoryId);

        await new DeleteNonConformanceHandler(db, user).Handle(
            new DeleteNonConformanceCommand(ncId), CancellationToken.None);

        var nc = await db.NonConformances.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == ncId);
        Assert.True(nc.IsDeleted);
    }

    [Fact]
    public async Task Get_ReturnsRowsInCreationOrder()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, _, premise) = await SeedHeaderReferencesAsync(db);
        var reportId = await SeedReportAsync(db, CompanyA, cb, scheme, [premise]);
        var categoryId = await SeedNonComplianceCategoryAsync(db);
        var first = await SeedNonConformanceAsync(db, CompanyA, reportId, categoryId);
        var second = await SeedNonConformanceAsync(db, CompanyA, reportId, categoryId);

        var rows = await new GetNonConformancesHandler(db, user).Handle(
            new GetNonConformancesQuery(reportId), CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal(first, rows[0].Id);
        Assert.Equal(second, rows[1].Id);
        Assert.Equal("Critical", rows[0].Category);
        Assert.Null(rows[0].CorrectiveActionDetails);
    }

    [Fact]
    public async Task Get_UnknownReport_ThrowsNotFound()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetNonConformancesHandler(db, user).Handle(
                new GetNonConformancesQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
