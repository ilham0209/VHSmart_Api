using VHSmart_Api.Features.Audit.AuditPlanning;
using static VHSmart_Api.Tests.Features.Audit.AuditPlanning.AuditPlanningTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditPlanning;

public class GetAuditPlanningCalendarTests
{
    private static GetAuditPlanningCalendarHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    private static async Task<(TestableVHSmartDbContext Db, Guid Premise, Guid Group, Guid Checklist, Guid Purpose, Guid Type, Guid Category)>
        SeedWorldAsync(TestableVHSmartDbContext db, Guid? companyId = null)
    {
        var company = companyId ?? CompanyA;
        var purpose = await SeedAuditPurposeAsync(db, company);
        var type = await SeedAuditTypeAsync(db, company);
        var category = await SeedPlanningCategoryAsync(db, company);
        var brand = await SeedBrandAsync(db, company);
        await SeedPrefixAsync(db, company, brand);
        var premise = await SeedPremiseAsync(db, company, brandId: brand);
        var checklist = await SeedChecklistAsync(db, company);
        var leader = await SeedAuditorAsync(db, company);
        var group = await SeedGroupAsync(db, company, checklist, leader);
        return (db, premise, group, checklist, purpose, type, category);
    }

    [Fact]
    public async Task Handle_Month_ReturnsPlansInRangeWithBarParts()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var world = await SeedWorldAsync(db);
        await SeedPlanAsync(
            db, CompanyA, world.Premise, world.Group, world.Checklist,
            world.Purpose, world.Type, world.Category);
        await SeedPlanAsync(
            db, CompanyA, world.Premise, world.Group, world.Checklist,
            world.Purpose, world.Type, world.Category,
            referenceNo: "SRN/STORE01/02-12-2026(1)",
            scheduleDate: new DateTime(2026, 12, 2));

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningCalendarQuery("month", new DateOnly(2026, 11, 10)),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 1), response.From);
        Assert.Equal(new DateOnly(2026, 11, 30), response.To);
        var bar = Assert.Single(response.Items);
        Assert.Equal(new DateOnly(2026, 11, 18), bar.ScheduleDate);
        Assert.Equal("Seri Rasa Factory", bar.PremiseName);
        Assert.Equal("STORE01", bar.StoreCode);
        Assert.Equal("TEAM ALPHA", bar.GroupName);
        Assert.Equal("SRN/STORE01/18-11-2026(1)", bar.AuditReferenceNo);
    }

    [Fact]
    public async Task Handle_Week_IsMondayToSunday()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var world = await SeedWorldAsync(db);
        // 2026-11-18 is a Wednesday; its week runs 16-22 November.
        await SeedPlanAsync(
            db, CompanyA, world.Premise, world.Group, world.Checklist,
            world.Purpose, world.Type, world.Category);
        await SeedPlanAsync(
            db, CompanyA, world.Premise, world.Group, world.Checklist,
            world.Purpose, world.Type, world.Category,
            referenceNo: "SRN/STORE01/23-11-2026(1)",
            scheduleDate: new DateTime(2026, 11, 23));

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningCalendarQuery("week", new DateOnly(2026, 11, 18)),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 16), response.From);
        Assert.Equal(new DateOnly(2026, 11, 22), response.To);
        var bar = Assert.Single(response.Items);
        Assert.Equal(new DateOnly(2026, 11, 18), bar.ScheduleDate);
    }

    [Fact]
    public async Task Handle_Day_ReturnsOnlyThatDay()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var world = await SeedWorldAsync(db);
        await SeedPlanAsync(
            db, CompanyA, world.Premise, world.Group, world.Checklist,
            world.Purpose, world.Type, world.Category);
        await SeedPlanAsync(
            db, CompanyA, world.Premise, world.Group, world.Checklist,
            world.Purpose, world.Type, world.Category,
            referenceNo: "SRN/STORE01/19-11-2026(1)",
            scheduleDate: new DateTime(2026, 11, 19));

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningCalendarQuery("day", new DateOnly(2026, 11, 18)),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 18), response.From);
        Assert.Equal(new DateOnly(2026, 11, 18), response.To);
        var bar = Assert.Single(response.Items);
        Assert.Equal(new DateOnly(2026, 11, 18), bar.ScheduleDate);
    }

    [Fact]
    public async Task Handle_ForeignRows_AreExcluded()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var worldB = await SeedWorldAsync(db, CompanyB);
        await SeedPlanAsync(
            db, CompanyB, worldB.Premise, worldB.Group, worldB.Checklist,
            worldB.Purpose, worldB.Type, worldB.Category,
            referenceNo: "FB/STORE01/18-11-2026(1)");

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningCalendarQuery("month", new DateOnly(2026, 11, 10)),
            CancellationToken.None);

        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task Handle_EmptyCompany_ReturnsEmptyItems()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningCalendarQuery("month", new DateOnly(2026, 11, 10)),
            CancellationToken.None);

        Assert.Empty(response.Items);
        Assert.Equal(new DateOnly(2026, 11, 1), response.From);
    }

    [Fact]
    public async Task Handle_Items_AreOrderedByScheduleDate()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var world = await SeedWorldAsync(db);
        await SeedPlanAsync(
            db, CompanyA, world.Premise, world.Group, world.Checklist,
            world.Purpose, world.Type, world.Category,
            referenceNo: "SRN/STORE01/20-11-2026(1)",
            scheduleDate: new DateTime(2026, 11, 20));
        await SeedPlanAsync(
            db, CompanyA, world.Premise, world.Group, world.Checklist,
            world.Purpose, world.Type, world.Category,
            referenceNo: "SRN/STORE01/10-11-2026(1)",
            scheduleDate: new DateTime(2026, 11, 10));

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningCalendarQuery("month", new DateOnly(2026, 11, 1)),
            CancellationToken.None);

        Assert.Equal(
            [new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 20)],
            response.Items.Select(item => item.ScheduleDate).ToList());
    }

    [Fact]
    public async Task Validator_UnknownView_Fails()
    {
        var validator = new GetAuditPlanningCalendarValidator();

        var result = await validator.ValidateAsync(
            new GetAuditPlanningCalendarQuery("quarter", new DateOnly(2026, 11, 18)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "View must be month, week or day.");
    }

    [Fact]
    public async Task Validator_EmptyDate_Fails()
    {
        var validator = new GetAuditPlanningCalendarValidator();

        var result = await validator.ValidateAsync(
            new GetAuditPlanningCalendarQuery("month", default));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.ErrorMessage == "Date is required.");
    }
}
