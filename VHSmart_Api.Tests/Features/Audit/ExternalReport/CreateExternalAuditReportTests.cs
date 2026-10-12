using Microsoft.EntityFrameworkCore;
using VHSmart_Api.Features.Audit.ExternalReport;
using VHSmart_Api.Shared.Domain.Audit;
using static VHSmart_Api.Tests.Features.Audit.ExternalReport.ExternalReportTestData;

namespace VHSmart_Api.Tests.Features.Audit.ExternalReport;

public class CreateExternalAuditReportTests
{
    private static CreateExternalAuditReportHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    private static CreateExternalAuditReportCommand Command(
        Guid certificationBodyId,
        Guid schemeId,
        string typeOfApplication,
        string typeOfInspection,
        params Guid[] premiseIds) => new(
        certificationBodyId,
        "HALAL-20251013-131025(PM)",
        typeOfApplication,
        typeOfInspection,
        schemeId,
        "APP-001",
        "CERT-001",
        new DateOnly(2025, 11, 29),
        new TimeSpan(9, 30, 0),
        new TimeSpan(12, 15, 0),
        "Routine external visit",
        "Pass",
        premiseIds);

    [Fact]
    public async Task Handle_ValidCommand_CreatesOpenedReportWithPremiseLinks()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, typeOfApplication, typeOfInspection, premise) =
            await SeedHeaderReferencesAsync(db);

        var response = await Handler(db, user).Handle(
            Command(cb, scheme, typeOfApplication, typeOfInspection, premise),
            CancellationToken.None);

        // The EXT reference format is [VERIFY] with no default - the column stays null.
        Assert.Null(response.AuditReferenceNo);
        Assert.Equal(ExternalAuditReportStatus.Opened, response.Status);
        Assert.Equal("JABATAN KEMAJUAN ISLAM MALAYSIA (JAKIM)", response.ReceivedFrom);
        Assert.Equal("Halal Food", response.ApplicationScheme);
        Assert.Equal(new DateOnly(2025, 11, 29), response.AuditDate);
        Assert.Equal(new TimeSpan(9, 30, 0), response.TimeIn);
        Assert.Equal("Pass", response.OverallStatus);
        var link = Assert.Single(response.Premises);
        Assert.Equal("Seri Rasa Factory", link.Name);

        var report = await db.ExternalAuditReports.SingleAsync(row => row.Id == response.Id);
        Assert.Equal(CompanyA, report.CompanyId);
        Assert.Equal("Opened", report.Status);
        Assert.Equal(
            1,
            await db.ExternalAuditReportPremises.CountAsync(
                row => row.ExternalAuditReportId == report.Id));
    }

    [Fact]
    public async Task Handle_DuplicatePremiseIds_AreDeduplicated()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, typeOfApplication, typeOfInspection, premise) =
            await SeedHeaderReferencesAsync(db);

        var response = await Handler(db, user).Handle(
            Command(cb, scheme, typeOfApplication, typeOfInspection, premise, premise),
            CancellationToken.None);

        Assert.Single(response.Premises);
    }

    [Fact]
    public async Task Handle_ForeignPremise_FailsValidation()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, typeOfApplication, typeOfInspection, _) =
            await SeedHeaderReferencesAsync(db);
        var foreignPremise = await SeedPremiseAsync(db, CompanyB);

        var results = await new CreateExternalAuditReportValidator(db, user)
            .ValidateAsync(Command(cb, scheme, typeOfApplication, typeOfInspection, foreignPremise));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Premise not found.");
    }

    [Fact]
    public async Task Handle_EmptyPremiseIds_FailsValidation()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, typeOfApplication, typeOfInspection, _) =
            await SeedHeaderReferencesAsync(db);

        var results = await new CreateExternalAuditReportValidator(db, user)
            .ValidateAsync(Command(cb, scheme, typeOfApplication, typeOfInspection));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Premise is required.");
    }

    [Fact]
    public async Task Handle_ForeignTypeOfApplication_FailsValidation()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, _, typeOfInspection, premise) = await SeedHeaderReferencesAsync(db);
        await SeedGeneralDataAsync(
            db, CompanyB, "External - Audit Category", "Foreign Value");

        var results = await new CreateExternalAuditReportValidator(db, user)
            .ValidateAsync(Command(cb, scheme, "Foreign Value", typeOfInspection, premise));

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Type of Application not found.");
    }

    [Fact]
    public async Task Handle_NotOverallStatusList_FailsValidation()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, typeOfApplication, typeOfInspection, premise) =
            await SeedHeaderReferencesAsync(db);
        var command = Command(cb, scheme, typeOfApplication, typeOfInspection, premise)
            with
        { OverallStatus = "Rejected" };

        var results = await new CreateExternalAuditReportValidator(db, user)
            .ValidateAsync(command);

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors,
            error => error.ErrorMessage == "Overall Status must be Pass, Fail or Pending.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validator_MissingCbAuditReference_Fails(string? reference)
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, typeOfApplication, typeOfInspection, premise) =
            await SeedHeaderReferencesAsync(db);
        var command = Command(cb, scheme, typeOfApplication, typeOfInspection, premise)
            with
        { CbAuditReferenceNo = reference! };

        var results = await new CreateExternalAuditReportValidator(db, user)
            .ValidateAsync(command);

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "CB Audit Reference No is required.");
    }

    [Fact]
    public async Task Validator_DefaultAuditDate_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, typeOfApplication, typeOfInspection, premise) =
            await SeedHeaderReferencesAsync(db);
        var command = Command(cb, scheme, typeOfApplication, typeOfInspection, premise)
            with
        { AuditDate = default };

        var results = await new CreateExternalAuditReportValidator(db, user)
            .ValidateAsync(command);

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Audit Date is required.");
    }

    [Fact]
    public async Task Validator_DefaultTimeIn_Fails()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var (cb, scheme, typeOfApplication, typeOfInspection, premise) =
            await SeedHeaderReferencesAsync(db);
        var command = Command(cb, scheme, typeOfApplication, typeOfInspection, premise)
            with
        { TimeIn = default };

        var results = await new CreateExternalAuditReportValidator(db, user)
            .ValidateAsync(command);

        Assert.False(results.IsValid);
        Assert.Contains(
            results.Errors, error => error.ErrorMessage == "Time In is required.");
    }
}
