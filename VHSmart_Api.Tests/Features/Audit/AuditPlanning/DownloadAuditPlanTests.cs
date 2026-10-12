using VHSmart_Api.Features.Audit.AuditPlanning;
using static VHSmart_Api.Tests.Features.Audit.AuditPlanning.AuditPlanningTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditPlanning;

public class DownloadAuditPlanTests
{
    private static DownloadAuditPlanHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_Day_ReturnsCsvHeaderAndThePlanRow()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var purpose = await SeedAuditPurposeAsync(db, name: "Routine Audit");
        var type = await SeedAuditTypeAsync(db, name: "Internal");
        var category = await SeedPlanningCategoryAsync(db, name: "PREMISE");
        var brand = await SeedBrandAsync(db);
        await SeedPrefixAsync(db, CompanyA, brand);
        var premise = await SeedPremiseAsync(db, CompanyA, name: "Seri Rasa Factory", brandId: brand);
        var checklist = await SeedChecklistAsync(db, CompanyA, name: "checklist tech");
        var leader = await SeedAuditorAsync(db, CompanyA, name: "LEAD ONE");
        var group = await SeedGroupAsync(db, CompanyA, checklist, leader);
        await SeedPlanAsync(
            db, CompanyA, premise, group, checklist, purpose, type, category);

        var download = await Handler(db, user).Handle(
            new DownloadAuditPlanQuery("day", new DateOnly(2026, 11, 18)),
            CancellationToken.None);

        Assert.Equal("audit-plan-day-2026-11-18.csv", download.FileName);
        var lines = download.Content.TrimEnd().Split("\r\n");
        Assert.Equal(
            "Audit Reference No,Schedule Date,Premise,Store Code,Group,Audit Purpose,Audit Type,Category,Audit Template,Assigned By,Date Assigned",
            lines[0]);
        // Assigned By is empty: SeedPlanAsync points the FK at an id no user row owns.
        Assert.Equal(
            "SRN/STORE01/18-11-2026(1),2026-11-18,Seri Rasa Factory,STORE01,TEAM ALPHA,Routine Audit,Internal,PREMISE,checklist tech,,2026-11-01",
            lines[1]);
    }

    [Fact]
    public async Task Handle_EmptyRange_ReturnsOnlyTheHeader()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var download = await Handler(db, user).Handle(
            new DownloadAuditPlanQuery("month", new DateOnly(2026, 11, 1)),
            CancellationToken.None);

        var lines = download.Content.TrimEnd().Split("\r\n");
        Assert.Single(lines);
        Assert.StartsWith("Audit Reference No,", lines[0]);
    }

    [Fact]
    public async Task Handle_PremiseNameWithComma_IsQuoted()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var purpose = await SeedAuditPurposeAsync(db);
        var type = await SeedAuditTypeAsync(db);
        var category = await SeedPlanningCategoryAsync(db);
        var brand = await SeedBrandAsync(db);
        await SeedPrefixAsync(db, CompanyA, brand);
        var premise = await SeedPremiseAsync(
            db, CompanyA, name: "Seri Rasa, Sdn Bhd", brandId: brand);
        var checklist = await SeedChecklistAsync(db);
        var leader = await SeedAuditorAsync(db, CompanyA);
        var group = await SeedGroupAsync(db, CompanyA, checklist, leader);
        await SeedPlanAsync(
            db, CompanyA, premise, group, checklist, purpose, type, category);

        var download = await Handler(db, user).Handle(
            new DownloadAuditPlanQuery("day", new DateOnly(2026, 11, 18)),
            CancellationToken.None);

        Assert.Contains("\"Seri Rasa, Sdn Bhd\"", download.Content);
    }

    [Fact]
    public async Task Handle_ForeignRows_AreExcluded()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var purpose = await SeedAuditPurposeAsync(db, CompanyB);
        var type = await SeedAuditTypeAsync(db, CompanyB);
        var category = await SeedPlanningCategoryAsync(db, CompanyB);
        var brand = await SeedBrandAsync(db, CompanyB);
        await SeedPrefixAsync(db, CompanyB, brand);
        var premise = await SeedPremiseAsync(db, CompanyB, brandId: brand);
        var checklist = await SeedChecklistAsync(db, CompanyB);
        var leader = await SeedAuditorAsync(db, CompanyB);
        var group = await SeedGroupAsync(db, CompanyB, checklist, leader);
        await SeedPlanAsync(
            db, CompanyB, premise, group, checklist, purpose, type, category,
            referenceNo: "FB/STORE01/18-11-2026(1)");

        var download = await Handler(db, user).Handle(
            new DownloadAuditPlanQuery("day", new DateOnly(2026, 11, 18)),
            CancellationToken.None);

        var lines = download.Content.TrimEnd().Split("\r\n");
        Assert.Single(lines);
    }

    [Fact]
    public async Task Validator_UnknownView_Fails()
    {
        var validator = new DownloadAuditPlanValidator();

        var result = await validator.ValidateAsync(
            new DownloadAuditPlanQuery("quarter", new DateOnly(2026, 11, 18)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "View must be month, week or day.");
    }
}
