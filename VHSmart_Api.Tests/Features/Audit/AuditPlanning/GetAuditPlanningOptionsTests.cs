using VHSmart_Api.Features.Audit.AuditPlanning;
using VHSmart_Api.Shared.Domain.Audit;
using static VHSmart_Api.Tests.Features.Audit.AuditPlanning.AuditPlanningTestData;

namespace VHSmart_Api.Tests.Features.Audit.AuditPlanning;

public class GetAuditPlanningOptionsTests
{
    private static GetAuditPlanningOptionsHandler Handler(
        TestableVHSmartDbContext db,
        TestCurrentUser user) => new(db, user);

    [Fact]
    public async Task Handle_ReturnsTheThreeGeneralDataPairs()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedAuditPurposeAsync(db, name: "Routine Audit");
        await SeedAuditTypeAsync(db, name: "Internal");
        await SeedPlanningCategoryAsync(db, name: "PREMISE");
        // An AUDIT-pair row that is NOT a planning dropdown must not leak into any list.
        await SeedNonPlanningCategoryAsync(db);

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningOptionsQuery(), CancellationToken.None);

        Assert.Equal("Routine Audit", Assert.Single(response.Purposes).Name);
        Assert.Equal("Internal", Assert.Single(response.Types).Name);
        Assert.Equal("PREMISE", Assert.Single(response.Categories).Name);
    }

    [Fact]
    public async Task Handle_Premises_AreOwnCompanyOnlyWithStoreCode()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedPremiseAsync(db, CompanyA, name: "Own Factory", storeCode: "SC1");
        await SeedPremiseAsync(db, CompanyB, name: "Foreign Factory");

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningOptionsQuery(), CancellationToken.None);

        var premise = Assert.Single(response.Premises);
        Assert.Equal("Own Factory", premise.Name);
        Assert.Equal("SC1", premise.StoreCode);
    }

    [Fact]
    public async Task Handle_Groups_OnlyActiveGroups()
    {
        // Only groups with status Active can be selected (spec 14.7/14.8 [CONFIRMED]).
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db);
        var leader = await SeedAuditorAsync(db, CompanyA);
        await SeedGroupAsync(
            db, CompanyA, checklist, leader, name: "ACTIVE TEAM",
            status: GroupAuditorStatus.Active);
        await SeedGroupAsync(
            db, CompanyA, checklist, leader, name: "IDLE TEAM",
            status: GroupAuditorStatus.Inactive);

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningOptionsQuery(), CancellationToken.None);

        Assert.Equal("ACTIVE TEAM", Assert.Single(response.Groups).Name);
    }

    [Fact]
    public async Task Handle_Groups_CarryTheChecklistLabel()
    {
        // The manual's Template Selection item: "{category} - {checklist name}".
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyA, name: "checklist tech");
        var leader = await SeedAuditorAsync(db, CompanyA);
        var group = await SeedGroupAsync(db, CompanyA, checklist, leader);

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningOptionsQuery(), CancellationToken.None);

        var option = Assert.Single(response.Groups);
        Assert.Equal(group, option.Id);
        Assert.Equal(checklist, option.ChecklistId);
        Assert.Equal("Technical Category - checklist tech", option.ChecklistLabel);
    }

    [Fact]
    public async Task Handle_Groups_ExcludeForeignRows()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        var checklist = await SeedChecklistAsync(db, CompanyB);
        var leader = await SeedAuditorAsync(db, CompanyB);
        await SeedGroupAsync(db, CompanyB, checklist, leader, name: "FOREIGN TEAM");

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningOptionsQuery(), CancellationToken.None);

        Assert.Empty(response.Groups);
    }

    [Fact]
    public async Task Handle_EmptyCompany_ReturnsEmptyLists()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningOptionsQuery(), CancellationToken.None);

        Assert.Empty(response.Purposes);
        Assert.Empty(response.Types);
        Assert.Empty(response.Categories);
        Assert.Empty(response.Premises);
        Assert.Empty(response.Groups);
    }

    [Fact]
    public async Task Handle_Options_SortByName()
    {
        var user = UserA();
        var db = await CreateDbAsync(user);
        await SeedAuditPurposeAsync(db, name: "Review");
        await SeedAuditPurposeAsync(db, name: "Routine Audit");

        var response = await Handler(db, user).Handle(
            new GetAuditPlanningOptionsQuery(), CancellationToken.None);

        Assert.Equal(["Review", "Routine Audit"],
            response.Purposes.Select(row => row.Name).ToList());
    }
}
